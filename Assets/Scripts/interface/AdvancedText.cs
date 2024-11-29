using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Game.Interface.Tags;
using TMPro;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Interface
{
    [RequireComponent(typeof(TMP_Text))]
    [ExecuteInEditMode]
    public class AdvancedText : MonoBehaviour
    {
        #region Base Classes
        public abstract class Tag
        {
            public int index;
            
            public abstract void LoadArgs(string raw, Args args);
            public virtual float GetTypingPause() => 0f;

            public virtual string GetOpeningPrefix() => null;
            public virtual string GetOpeningSuffix() => null;
        }

        public abstract class ClosableTag : Tag
        {
            public int length;

            public abstract void ApplyTextEffects(AdvancedText advancedText, TMP_TextInfo text);
            public virtual float GetTypingSpeedMultiplier() => 1f;
            public virtual ITypingAnimation GetTypingAnimation() => null;
            
            public virtual string GetClosingPrefix() => null;
            public virtual string GetClosingSuffix() => null;
        }

        public interface ITypingAnimation
        {
            public void ApplyTypingAnimation(AdvancedText advancedText, TMP_TextInfo text, TypingInfo typingInfo, int index);
        }
        #endregion

        #region Basic Classes
        public class TypingInfo
        {
            public CharTypingInfo[] typingTimes;
            public ITypingAnimation[] typingAnimations;
            public float totalTypingTime;
        }

        public struct CharTypingInfo
        {
            public float start;
            public float length;
        }
        
        public class Args : Dictionary<string, string>
        {
            public float GetFloat(string key, float fallback)
            {
                if (TryGetValue(key, out var value) && float.TryParse(value, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var result))
                    return result;

                return fallback;
            }
            
            public int GetInt(string key, int fallback)
            {
                if (TryGetValue(key, out var value) && int.TryParse(value, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var result))
                    return result;

                return fallback;
            }
            
            public bool GetBool(string key, bool fallback)
            {
                if (TryGetValue(key, out var value) && bool.TryParse(value, out var result))
                    return result;

                return fallback;
            }
            
            public string GetString(string key, string fallback)
            {
                return TryGetValue(key, out var value) ? value : fallback;
            }
            
            public T GetEnum<T>(string key, T fallback) where T : struct, Enum
            {
                if (TryGetValue(key, out var value) && Enum.TryParse(value, out T result))
                    return result;

                return fallback;
            }
        }
        #endregion
        
        #region Internal Classes
        private class AdvancedTextPreprocessor : ITextPreprocessor
        {
            private const float TypingTimeDefault = 0.02f;
            private const float TypingTimeWhiteSpace = TypingTimeDefault * 2f;
            private const float TypingTimePunctuation = TypingTimeDefault * 8f;
            private const float TypingTimePunctuationLong = TypingTimePunctuation * 3f;
            private const float TypingTimeLineBreak = TypingTimePunctuationLong * 2f;
            
            private delegate Tag Factory();
            private static readonly Regex _TagRegex = new(@"\<(?<tag_name>\/?\w+)(?:\s*(?<arg_name>\w*)=(?:""(?<arg_value>[^""]*)""|(?<arg_value>[^\s>]*)))*\>");
            
            //Internal buffers
            private string _lastText;
            private string _lastPreprocessedText;
            private readonly Dictionary<string, Factory> _tags = new();
            
            //Public data
            public TypingInfo typingInfo;
            public Tag[] tags = Array.Empty<Tag>();
            
            public AdvancedTextPreprocessor()
            {
                _tags["rainbow"] = () => new RainbowAdvancedTextTag();
                _tags["shake"] = () => new ShakeAdvancedTextTag();
                _tags["pause"] = () => new PauseAdvancedTextTag();
                _tags["slow"] = () => new TypingSpeedAdvancedTextTag{speed = 0.25f};
                _tags["fast"] = () => new TypingSpeedAdvancedTextTag{speed = 4f};
                _tags["wave"] = () => new WaveAdvancedTextTag();
            }

            public string PreprocessText(string text)
            {
                if (text == _lastText)
                    return _lastPreprocessedText;
                
                //Results
                var tagsToClose = new Stack<ClosableTag>();
                var tagList = new List<Tag>();
                var argsDict = new Args();
                var typingTimes = new List<CharTypingInfo>();
                var typingAnimations = new List<ITypingAnimation>();
                var typingMaxTime = 0f;
               
                //Parsing
                var resultString = new StringBuilder();
                var lastIndex = 0;
                var baked = 0;
                var offset = 0;
                var pauseToAdd = 0f;
                var lastTime = 0f;

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                void AddChar(char c)
                {
                    var typingAnimation = default(ITypingAnimation);
                    var multiplier = 1f;
                           
                    for (var j = 0; j < tagsToClose.Count; j++)
                    {
                        var ctg = tagsToClose.ToArray()[j];
                        typingAnimation = ctg.GetTypingAnimation() ?? typingAnimation;
                        multiplier *= ctg.GetTypingSpeedMultiplier();
                    }
                    
                    var iMultiplier = 1f / multiplier;
                    var start = lastTime + pauseToAdd;
                    lastTime = start + _GetTimeForChar(c) * iMultiplier;
                                    
                    typingTimes.Add(new CharTypingInfo{start = start, length = TypingTimeDefault * iMultiplier});
                    typingAnimations.Add(typingAnimation);
                    typingMaxTime = Math.Max(typingMaxTime, lastTime);
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                void BakeTag(int openIndex, int closeIndex, bool closed)
                {
                    var tag = resultString.ToString(openIndex, closeIndex - openIndex + 1);
                    
                    if (!closed)
                    {
                        for (var i = openIndex; i <= closeIndex; i++)
                            AddChar(resultString[i]);

                        return;
                    }
            
                    //For now, don't check if the tag is valid or not (the name, args, and if it's closable)  
                    offset -= tag.Length;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                void Bake()
                {
                    var tagOpenIndex = -1;

                    for (var i = baked; i < resultString.Length; i++)
                    {
                        var c = resultString[i];
                        switch (c)
                        {
                            case '<':
                                if (tagOpenIndex != -1)
                                    BakeTag(tagOpenIndex, i, false);
                                tagOpenIndex = i;
                                break;  
                            case '>':
                                if (tagOpenIndex != -1)
                                {
                                    BakeTag(tagOpenIndex, i, true);
                                    tagOpenIndex = -1;
                                }
                                else
                                    AddChar(c);
                                break;
                            default:
                                if (tagOpenIndex == -1)
                                {
                                    if (i > 0 && char.IsSurrogatePair(resultString[i - 1], c))
                                        offset--;
                                    else
                                        AddChar(c);
                                }
                                break;
                        }
                    }
                    

                    if (tagOpenIndex != -1)
                    {
                        BakeTag(tagOpenIndex, resultString.Length - 1, false);
                    }
                    
                    baked = resultString.Length;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                void InsertText(string insert)
                {
                    if (insert == null)
                        return;
                    
                    resultString.Append(insert);
                    offset += insert.Length;
                }
                
                foreach (Match match in _TagRegex.Matches(text))
                {
                    var tag = match.Groups["tag_name"].Value;
                    var args = match.Groups["arg_name"].Captures;
                    var values = match.Groups["arg_value"].Captures;
                    var closing = tag[0] == '/';
                    
                    if (!_tags.TryGetValue(tag[(closing ? 1 : 0)..], out var factory))
                        continue;

                    //Keep the text before the tag
                    resultString.Append(text, lastIndex, match.Index - lastIndex);
                    lastIndex = match.Index + match.Length;
                    
                    if (closing)
                    {
                        if (tagsToClose.Count <= 0 || tagsToClose.Peek().GetType() != factory().GetType())
                            continue;

                        var ctg = tagsToClose.Peek();
                        
                        InsertText(ctg.GetClosingPrefix());
                        Bake();
                        
                        var index = match.Index + offset;
                        offset -= match.Length;
                        
                        ctg.length = index - ctg.index;
                        
                        tagsToClose.Pop();
                        tagList.Add(ctg);
                        
                        InsertText(ctg.GetClosingSuffix());
                    }
                    else
                    {
                        argsDict.Clear();
                        for (var i = 0; i < args.Count; i++)
                        {
                            var arg = args[i].Value;
                            var value = values[i].Value;
                            argsDict[arg] = value;
                        }
                        
                        var tg = factory();
                        
                        InsertText(tg.GetOpeningPrefix());
                        Bake();
                        
                        var index = match.Index + offset;
                        offset -= match.Length;
                        
                        tg.index = index;
                        tg.LoadArgs(match.Value, argsDict);
                        pauseToAdd += tg.GetTypingPause();
                        
                        if (tg is ClosableTag ctg)
                            tagsToClose.Push(ctg);
                        else
                            tagList.Add(tg);

                        InsertText(tg.GetOpeningSuffix());
                    }
                }
                
                //Finalize parsing data
                resultString.Append(text, lastIndex, text.Length - lastIndex);
                Bake();
                
                //Save data
                tags = tagList.ToArray();
                typingInfo = new TypingInfo
                {
                    typingTimes = typingTimes.ToArray(),
                    totalTypingTime = typingMaxTime,
                    typingAnimations = typingAnimations.ToArray()
                };
                
                _lastText = text;
                return _lastPreprocessedText = resultString.ToString();
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private float _GetTimeForChar(char c)
            {
                return c switch
                {
                    '.' => TypingTimePunctuationLong,
                    ',' => TypingTimePunctuation,
                    ';' => TypingTimePunctuation,
                    ':' => TypingTimePunctuation,
                    '—' => TypingTimePunctuationLong,
                    ' ' => TypingTimeWhiteSpace,
                    '\n' => TypingTimeLineBreak,
                    _ => TypingTimeDefault
                };
            }
        }
        #endregion

        #region Typing Animations
        private class StandardTypingAnimation : ITypingAnimation
        {
            public void ApplyTypingAnimation(AdvancedText advancedText, TMP_TextInfo text, TypingInfo typingInfo, int index)
            {
                var time = advancedText.time;
                var textInfo = text.characterInfo;
            
                var charInfo = textInfo[index];

                var materialIndex = charInfo.materialReferenceIndex;
                var vertices = text.meshInfo[materialIndex].vertices;
                var vertexIndex = charInfo.vertexIndex;
                
                var topLeft = vertices[vertexIndex + 0];
                var bottomRight = vertices[vertexIndex + 2];
                var center = (topLeft + bottomRight) / 2f;

                var charTypingInfo = typingInfo.typingTimes[index];
                
                float scale = 0;
                
                if (time > charTypingInfo.start)
                {
                    if (time < charTypingInfo.start + charTypingInfo.length)
                        scale = (time - charTypingInfo.start) / charTypingInfo.length;
                    else
                        scale = 1;
                }
                
                vertices[vertexIndex] = _ScaleVertex(topLeft, center, scale);
                vertices[vertexIndex + 1] = _ScaleVertex(vertices[vertexIndex + 1], center, scale);
                vertices[vertexIndex + 2] = _ScaleVertex(bottomRight, center, scale);
                vertices[vertexIndex + 3] = _ScaleVertex(vertices[vertexIndex + 3], center, scale);
            }
            
            private static Vector3 _ScaleVertex(Vector3 vertex, Vector3 center, float scale)
            {
                var dir = vertex - center;
                dir *= scale;
                return center + dir;
            }
        }
        #endregion
        
        #region Inspector Fields
        public float time = 1f;
        public float typingSpeed = 1f;
        #endregion

        #region Helper Properties
        public float TotalTypingTime => _textPreprocessor.typingInfo?.totalTypingTime ?? 0f;
        #endregion
        
        private readonly AdvancedTextPreprocessor _textPreprocessor = new();
        private readonly ITypingAnimation _anim = new StandardTypingAnimation();
        private TMP_Text _text;

        private void OnEnable()
        {
            _text = GetComponent<TMP_Text>();
            
            if (_text == null)
                return;
            
            _text.textPreprocessor = _textPreprocessor;
        }
        
        public void Update()
        {
            if (_text == null)
                return;
            
            _text.ForceMeshUpdate();

            if(Application.isPlaying)
                time += Time.deltaTime * typingSpeed;
            
            //Tags
            foreach (var tg in _textPreprocessor.tags)
                if (tg is ClosableTag ctg)
                    ctg.ApplyTextEffects(this, _text.textInfo);
            
            //Typing Animation
            var textInfo = _text.textInfo;
            var characterCount = textInfo.characterCount;
            
            for (var i = 0; i < characterCount; i++)
            {
                var charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible)
                    continue;
                
                var anim = _textPreprocessor.typingInfo.typingAnimations[i] ?? _anim;
                anim.ApplyTypingAnimation(this, textInfo, _textPreprocessor.typingInfo, i);
            }

            _text.UpdateVertexData();    
        }
    }
    
    //custom editor, show time as a slider
    #if UNITY_EDITOR
    [CustomEditor(typeof(AdvancedText))]
    public class AdvancedTextEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            var advancedText = (AdvancedText) target;
            advancedText.time = EditorGUILayout.Slider("Time", advancedText.time, 0f, advancedText.TotalTypingTime);
            advancedText.typingSpeed = EditorGUILayout.Slider("Typing Speed", advancedText.typingSpeed, 0f, 10f);
            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(advancedText);
            
            EditorGUILayout.HelpBox($"Total Typing Time: {advancedText.TotalTypingTime:0.000}s", MessageType.Info);
        }
    }
    #endif
}

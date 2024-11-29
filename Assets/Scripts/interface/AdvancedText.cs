using System;
using System.Collections.Generic;
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
            
            public abstract void LoadArgs(string raw, IReadOnlyDictionary<string, string> args);
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
            public float[] typingTimes;
            public ITypingAnimation[] typingAnimations;
            public float totalTypingTime;
        }
        
        private class AdvancedTextPreprocessor : ITextPreprocessor
        {
            private const float TypingTimeDefault = 0.05f;
            private const float TypingTimeWhiteSpace = TypingTimeDefault * 2f;
            private const float TypingTimePunctuation = TypingTimeDefault * 5f;
            private const float TypingTimeLineBreak = TypingTimeDefault * 10f;
            
            private delegate Tag Factory();
            private static readonly Regex _TagRegex = new(@"\<(?<tag_name>\/?\w+)(?:\s*(?<arg_name>\w*)=(?:""(?<arg_value>[^""]*)""|(?<arg_value>[^\s]+)))*\>");
            
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
            }

            public string PreprocessText(string text)
            {
                if (text == _lastText)
                    return _lastPreprocessedText;
                
                //Results
                var tagsToClose = new Stack<ClosableTag>();
                var tagList = new List<Tag>();
                var argsDict = new Dictionary<string, string>();

                //Parsing
                var resultString = new StringBuilder();
                var lastIndex = 0;
                var offset = 0;
                var discarded = 0;
                
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                void Discard()
                {
                    var tagOpen = false;
                    
                    for (var i = discarded; i < resultString.Length; i++)
                    {
                        var c = resultString[i];
                        switch (c)
                        {
                            case '<':
                                tagOpen = true;
                                offset--;
                                break;
                            case '>':
                                tagOpen = false;
                                offset--;
                                break;
                            default:
                                if (tagOpen)
                                    offset--;
                                else if (i > 0 && char.IsSurrogatePair(resultString[i - 1], c))
                                    offset--;
                                break;
                        }
                    }
                    
                    discarded = resultString.Length;
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

                        var ctg = tagsToClose.Pop();
                        
                        InsertText(ctg.GetClosingPrefix());
                        Discard();
                        
                        var index = match.Index + offset;
                        offset -= match.Length;
                        
                        ctg.length = index - ctg.index;
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
                        Discard();
                        
                        var index = match.Index + offset;
                        offset -= match.Length;
                        
                        tg.index = index;
                        tg.LoadArgs(match.Value, argsDict);
                        
                        if (tg is ClosableTag ctg)
                            tagsToClose.Push(ctg);
                        else
                            tagList.Add(tg);

                        InsertText(tg.GetOpeningSuffix());
                    }
                }
                
                //Finalize parsing data
                resultString.Append(text, lastIndex, text.Length - lastIndex);
                Discard();
                tags = tagList.ToArray();
                
                //Generating typing animation
                var typingTimes = new float[text.Length + offset + 1];
                var typingAnimations = new ITypingAnimation[text.Length + offset];
                var typingTimesMultiplier = new float[text.Length + offset];
                var typingTotalTime = 0f;
                
                //Pause times
                foreach (var tg in tagList)
                {
                    var pause = tg.GetTypingPause();
                    typingTimes[tg.index] += pause;
                }
                
                //Calculate tag speed and animation influence
                foreach (var tg in tagList)
                {
                    if (tg is not ClosableTag ctg)
                        continue;
                    
                    //Animations
                    var anim = ctg.GetTypingAnimation();
                    if (anim != null)
                        for (var i = ctg.index; i < ctg.index + ctg.length; i++)
                            typingAnimations[i] = anim;
                    
                    //Speed Multiplier
                    var tsm = ctg.GetTypingSpeedMultiplier();
                    if (Math.Abs(tsm - 1f) > 0.001f)
                        for (var i = ctg.index; i < ctg.index + ctg.length; i++)
                            typingTimesMultiplier[i] = (typingTimesMultiplier[i] + 1) * (tsm / 1f) - 1f;
                }

                //Bake typing times
                offset = 0;
                var tagOpen = false;
                
                for (var i = 0; i < resultString.Length; i++)
                {
                    var c = resultString[i];
                    switch (c)
                    {
                        case '<':
                            tagOpen = true;
                            offset--;
                            break;
                        case '>':
                            tagOpen = false;
                            offset--;
                            break;
                        default:
                            if (tagOpen)
                                offset--;
                            else if (i > 0 && char.IsSurrogatePair(resultString[i - 1], c))
                                offset--;
                            else
                            {
                                var index = i + offset;
                                typingTimes[index + 1] += typingTimes[index] + _GetTimeForChar(c) * (typingTimesMultiplier[index] + 1f);
                                typingTotalTime = Math.Max(typingTotalTime, typingTimes[index + 1]);
                            }
                            break;
                    }
                }
                
                typingInfo = new TypingInfo
                {
                    typingTimes = typingTimes,
                    totalTypingTime = typingTotalTime,
                    typingAnimations = typingAnimations
                };
                
                _lastText = text;
                return _lastPreprocessedText = resultString.ToString();
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private float _GetTimeForChar(char c)
            {
                return c switch
                {
                    '.' => TypingTimePunctuation,
                    ',' => TypingTimePunctuation,
                    ';' => TypingTimePunctuation,
                    ':' => TypingTimePunctuation,
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

                var startTime = typingInfo.typingTimes[index];
                
                float scale = 0;
                
                if (time > startTime)
                { 
                    var endTime = typingInfo.typingTimes[index + 1];
                    
                    if (time < endTime)
                        scale = (time - startTime) / (endTime - startTime);
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

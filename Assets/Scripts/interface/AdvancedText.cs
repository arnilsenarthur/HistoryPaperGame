using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.Interface.Tags;
using TMPro;
using UnityEngine;

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
            public int textIndex;
            
            public abstract void LoadArgs(string raw, IReadOnlyDictionary<string, string> args);
            public virtual float GetTypingPause() => 0f;
        }

        public abstract class ClosableTag : Tag
        {
            public int length;
            public int textLength;

            public abstract void ApplyTextEffects(AdvancedText advancedText, TMP_TextInfo text);
            public virtual float GetTypingSpeedMultiplier() => 1f;
            public virtual ITypingAnimation GetTypingAnimation() => null;
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
            private static readonly Regex _TagRegex = new(@"\[(?<tag_name>\:?\w+)(?:\s+(?<arg_name>\w+)=(?:""(?<arg_value>[^""]*)""|(?<arg_value>[^\s]+)))*\]");
            private static readonly Dictionary<string, Factory> _Tags = new();

            public Tag[] tags = Array.Empty<Tag>();
            private string _lastText;
            private string _lastPreprocessedText;
            
            public TypingInfo typingInfo;
            
            public AdvancedTextPreprocessor()
            {
                _Tags["rainbow"] = () => new RainbowAdvancedTextTag();
                _Tags["shake"] = () => new ShakeAdvancedTextTag();
                _Tags["pause"] = () => new PauseAdvancedTextTag();
                _Tags["slow"] = () => new TypingSpeedAdvancedTextTag{speed = 0.25f};
                _Tags["fast"] = () => new TypingSpeedAdvancedTextTag{speed = 4f};
            }

            public string PreprocessText(string text)
            {
                if (text == _lastText)
                    return _lastPreprocessedText;
                _lastText = text;
                
                var offset = 0;
                
                var dict = new Dictionary<string, string>();
                
                var tagList = new List<Tag>();
                var tagsToClose = new Stack<ClosableTag>();
                var discardLastCheck = 0;
                var discard = 0;

                foreach (Match match in _TagRegex.Matches(text))
                {
                    var tag = match.Groups["tag_name"].Value;
                    var args = match.Groups["arg_name"].Captures;
                    var values = match.Groups["arg_value"].Captures;
                    var closing = tag[0] == ':';
                    
                    //Invalid tag
                    if (!_Tags.TryGetValue(tag[(closing ? 1 : 0)..], out var factory)) 
                        continue;
                    
                    if (closing)
                    {
                        if (tagsToClose.Count <= 0 || tagsToClose.Peek().GetType() != factory().GetType())
                            continue;
                            
                        var ctg = tagsToClose.Pop();
                        var index = match.Index - offset;
                            
                        //Discard invalid characters
                        for (var i = discardLastCheck + 1; i < index; i++)
                            if (char.IsSurrogatePair(text[i - 1], text[i]))
                                discard++;
                        discardLastCheck = index;

                        //Set the length of the tag
                        ctg.length = index - ctg.textIndex - discard + (ctg.textIndex - ctg.index);
                        ctg.textLength = index - ctg.textIndex;
                            
                        //Remove the tag from the text
                        text = text.Remove(index, match.Length);
                        offset += match.Length;
                    }
                    else
                    {
                        //Remove the tag from the text
                        var index = match.Index - offset;
                        text = text.Remove(index, match.Length);
                        offset += match.Length;
                            
                        //Args
                        dict.Clear();
                        for (var i = 0; i < args.Count; i++)
                        {
                            var arg = args[i].Value;
                            var value = values[i].Value;
                            dict[arg] = value;
                        }
                            
                        //Discard invalid characters
                        for (var i = discardLastCheck + 1; i < index; i++)
                            if (char.IsSurrogatePair(text[i - 1], text[i]))
                                discard++;
                        discardLastCheck = index;
                            
                        //Create the tag
                        var tg = factory();
                        tg.textIndex = index;
                        tg.index = index - discard;
                        tg.LoadArgs(match.Value, dict);

                        tagList.Add(tg);

                        if (tg is ClosableTag ctg)
                            tagsToClose.Push(ctg);
                    }
                }

                //Generating typing animation
                var typingTimes = new float[text.Length + 1];
                var typingAnimations = new ITypingAnimation[text.Length];
                
                //Pausing times
                foreach (var tg in tagList)
                {
                    var pause = tg.GetTypingPause();
                    typingTimes[tg.textIndex] += pause;
                }
                
                var tempTypingMultipliers = new float[text.Length];
                
                foreach (var tg in tagList)
                {
                    if (tg is not ClosableTag ctg)
                        continue;
                    
                    //Animations
                    var anim = ctg.GetTypingAnimation();
                    if (anim != null)
                        for (var i = ctg.textIndex; i < ctg.textIndex + ctg.textLength; i++)
                            typingAnimations[i] = anim;
                    
                    //Speed Multiplier
                    var tsm = ctg.GetTypingSpeedMultiplier();
                    if (Math.Abs(tsm - 1f) > 0.001f)
                        for (var i = ctg.textIndex; i < ctg.textIndex + ctg.textLength; i++)
                            tempTypingMultipliers[i] = (tempTypingMultipliers[i] + 1) * (tsm / 1f) - 1f;
                }
                
                //Starting time, 0 is default time
                for(var i = 0; i < text.Length; i++)
                {
                    var multiplier = 1f / (tempTypingMultipliers[i] + 1f);
                    
                    switch (text[i])
                    {
                        case '.':
                        case ',':
                        case ';':
                        case ':':
                            typingTimes[i + 1] += typingTimes[i] + TypingTimePunctuation * multiplier;
                            break;
                        
                        case ' ':
                            typingTimes[i + 1] += typingTimes[i] + TypingTimeWhiteSpace * multiplier;
                            break;
                        
                        case '\n':
                            typingTimes[i + 1] += typingTimes[i] + TypingTimeLineBreak * multiplier;
                            break;
                        
                        default:
                            typingTimes[i + 1] += typingTimes[i] + TypingTimeDefault * multiplier;
                            break;
                    }
                }
 
                typingInfo = new TypingInfo
                {
                    typingTimes = typingTimes,
                    totalTypingTime = typingTimes[^1],
                    typingAnimations = typingAnimations
                };
                
                tags = tagList.ToArray();
                
                return _lastPreprocessedText = text;
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

                var startTime = typingInfo.typingTimes[charInfo.index];
                
                float scale = 0;
                
                if (time > startTime)
                { 
                    var endTime = typingInfo.typingTimes[charInfo.index + 1];
                    
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
        [Range(0f, 10f)]
        public float time = 1f;
        [Range(0f, 10f)]
        public float typingSpeed = 1f;
        #endregion
        
        private AdvancedTextPreprocessor _textPreprocessor;
        private ITypingAnimation _anim;
        private TMP_Text _text;

        private void OnEnable()
        {
            _text = GetComponent<TMP_Text>();
            
            if (_text == null)
                return;
            
            _anim = new StandardTypingAnimation();
            _text.textPreprocessor = _textPreprocessor = new AdvancedTextPreprocessor();
        }
        
        public void Update()
        {
            if (_text == null)
                return;
            
            _text.ForceMeshUpdate();

            if(Application.isPlaying)
                time += Time.deltaTime * typingSpeed;
            
            var textInfo = _text.textInfo;
            var characterCount = textInfo.characterCount;
            
            for (var i = 0; i < characterCount; i++)
            {
                var charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible)
                    continue;
                
                var anim = _textPreprocessor.typingInfo.typingAnimations[charInfo.index] ?? _anim;
                anim.ApplyTypingAnimation(this, textInfo, _textPreprocessor.typingInfo, i);
            }

            foreach (var tg in _textPreprocessor.tags)
                if (tg is ClosableTag ctg)
                    ctg.ApplyTextEffects(this, _text.textInfo);

            _text.UpdateVertexData();    
        }
    }
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Game.Interface.Tags
{
    public class ShakeAdvancedTextTag : AdvancedText.ClosableTag
    {
        public override void LoadArgs(string raw, IReadOnlyDictionary<string, string> args)
        {
            
        }

        public override void ApplyTextEffects(AdvancedText advancedText, TMP_TextInfo text)
        {
            for (var i = index; i < index + length; i++)
            {
                var charInfo = text.characterInfo[i];
                
                if (!charInfo.isVisible)
                    continue;

                var materialIndex = charInfo.materialReferenceIndex;
                var meshInfo = text.meshInfo[materialIndex];
                var vertices = meshInfo.vertices;
                var vertexIndex = charInfo.vertexIndex;
                
                var offset = new Vector3(0f, Mathf.Sin(Time.time * 10f + i / 100f), 0f);
                
                vertices[vertexIndex] += offset;
                vertices[vertexIndex + 1] += offset;
                vertices[vertexIndex + 2] += offset;
                vertices[vertexIndex + 3] += offset;
            }
        }
    }   
}
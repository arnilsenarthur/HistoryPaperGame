using TMPro;
using UnityEngine;

namespace Game.Interface.Tags
{
    public class ShakeAdvancedTextTag : AdvancedText.ClosableTag
    {
        public float speed = 1f;
        public float strength = 1f;
        public float randomness = 1f;
    
        public override void LoadArgs(string raw, AdvancedText.Args args)
        {
            speed = args.GetFloat("speed", speed);
            strength = args.GetFloat("strength", strength);
            randomness = args.GetFloat("randomness", randomness);
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
                
                var offset = new Vector3(Mathf.Cos(Time.time * speed + i * randomness) * strength, Mathf.Sin(Time.time * speed + i * randomness) * strength, 0f);
                
                vertices[vertexIndex] += offset;
                vertices[vertexIndex + 1] += offset;
                vertices[vertexIndex + 2] += offset;
                vertices[vertexIndex + 3] += offset;
            }
        }
    }   
}
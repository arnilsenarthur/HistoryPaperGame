using TMPro;
using UnityEngine;

namespace Game.Interface.Tags
{
    public class RainbowAdvancedTextTag : AdvancedText.ClosableTag
    {
        public float scale = 1f;
        public float speed = 1f;
        
        public override void LoadArgs(string raw, AdvancedText.Args  args)
        {
            scale = args.GetFloat("scale", scale);
            speed = args.GetFloat("speed", speed);
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
                var colors = meshInfo.colors32;
                var vertices = meshInfo.vertices;
                var vertexIndex = charInfo.vertexIndex;
                
                colors[vertexIndex] = _SampleColor(vertices[vertexIndex]);
                colors[vertexIndex + 1] = _SampleColor(vertices[vertexIndex + 1]);
                colors[vertexIndex + 2] = _SampleColor(vertices[vertexIndex + 2]);
                colors[vertexIndex + 3] = _SampleColor(vertices[vertexIndex + 3]);
            }
        }
        
        private Color32 _SampleColor(Vector2 position)
        {
            return Color.HSVToRGB(Mathf.Repeat(Time.time * speed * 0.25f + position.x * scale * 0.01f, 1f), 1f, 1f);
        }
        
        public override AdvancedText.ITypingAnimation GetTypingAnimation()
        {
            return new ScreamingAdvancedTextTypingAnimation();
        }
    }
}
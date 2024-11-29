using UnityEngine;

namespace Game.Interface.Tags
{
    public class WaveAdvancedTextTag : AdvancedText.ClosableTag
    {
        public float scale = 1f;
        public float speed = 1f;
        public float height = 1f;
        
        public override void LoadArgs(string raw, AdvancedText.Args  args)
        {
            scale = args.GetFloat("scale", scale) * 2f;
            speed = args.GetFloat("speed", speed) * 5f;
            height = args.GetFloat("height", height) * 2f;
        }

        public override void ApplyTextEffects(AdvancedText advancedText, TMPro.TMP_TextInfo text)
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
                
                var offset = new Vector3(0f, Mathf.Sin(Time.time * speed + i * scale) * height, 0f);
                
                vertices[vertexIndex] += offset;
                vertices[vertexIndex + 1] += offset;
                vertices[vertexIndex + 2] += offset;
                vertices[vertexIndex + 3] += offset;
            }
        }

        public override AdvancedText.ITypingAnimation GetTypingAnimation()
        {
            return new ScreamingAdvancedTextTypingAnimation();
        }
    }
}
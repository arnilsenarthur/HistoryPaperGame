using TMPro;
using UnityEngine;

namespace Game.Interface.Tags
{
    public class StrongAdvancedTextTypingAnimation : AdvancedText.ITypingAnimation
    {
        public void ApplyTypingAnimation(AdvancedText advancedText, TMP_TextInfo text, AdvancedText.TypingInfo typingInfo, int index)
        {
            var time = advancedText.typingTime;
            var textInfo = text.characterInfo;
            
            var charInfo = textInfo[index];

            var materialIndex = charInfo.materialReferenceIndex;
            var vertices = text.meshInfo[materialIndex].vertices;
            var colors = text.meshInfo[materialIndex].colors32;
            var vertexIndex = charInfo.vertexIndex;
                
            var topLeft = vertices[vertexIndex + 0];
            var bottomRight = vertices[vertexIndex + 2];
            var center = (topLeft + bottomRight) / 2f;

            var charTypingInfo = typingInfo.typingTimes[index];
                
            float dt = 0;
                
            if (time > charTypingInfo.start)
            {
                if (time < charTypingInfo.start + charTypingInfo.length)
                    dt = (time - charTypingInfo.start) / charTypingInfo.length;
                else
                    dt = 1;
            }
            
            var scale = Mathf.Lerp(1.5f, 1f, dt);
                
            vertices[vertexIndex] = _ScaleVertex(topLeft, center, scale);
            vertices[vertexIndex + 1] = _ScaleVertex(vertices[vertexIndex + 1], center, scale);
            vertices[vertexIndex + 2] = _ScaleVertex(bottomRight, center, scale);
            vertices[vertexIndex + 3] = _ScaleVertex(vertices[vertexIndex + 3], center, scale);
            
            colors[vertexIndex] = _Alpha(colors[vertexIndex], dt);
            colors[vertexIndex + 1] = _Alpha(colors[vertexIndex + 1], dt);
            colors[vertexIndex + 2] = _Alpha(colors[vertexIndex + 2], dt);
            colors[vertexIndex + 3] = _Alpha(colors[vertexIndex + 3], dt);
        }
        
        private static Color _Alpha(Color color, float alpha)
        {
            color.a *= alpha;
            return color;
        }
        
        private static Vector3 _ScaleVertex(Vector3 vertex, Vector3 center, float scale)
        {
            var dir = vertex - center;
            dir *= scale;
            return center + dir;
        }
    }
}
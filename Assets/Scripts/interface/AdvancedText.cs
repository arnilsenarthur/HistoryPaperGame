using TMPro;
using UnityEngine;

namespace Game.Interface
{
    [ExecuteInEditMode]
    public class AdvancedText : MonoBehaviour
    {
        #region Inspector Fields
        public TMP_Text text;
        #endregion

        [Range(0.0001f, 1f)]
        public float scale = 1f;

        public void Update()
        {
            if (text == null)
                return;
            
            text.ForceMeshUpdate();
            
            var textInfo = text.textInfo;
            var characterCount = textInfo.characterCount;
            
            var charScale = (scale * characterCount) % 1;
            var charIndex = (int) (scale * characterCount);
            
            for (var i = 0; i < characterCount; i++)
            {
                var charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible)
                    continue;
                
                var materialIndex = charInfo.materialReferenceIndex;
                var vertices = textInfo.meshInfo[materialIndex].vertices;
                var vertexIndex = charInfo.vertexIndex;
                
                var topLeft = vertices[vertexIndex + 0];
                var bottomRight = vertices[vertexIndex + 2];
                var center = (topLeft + bottomRight) / 2f;
                
                var sc = i == charIndex ? charScale : (i < charIndex ? 1 : 0);
                
                vertices[vertexIndex + 0] = ScaleVertex(topLeft, center, sc);
                vertices[vertexIndex + 1] = ScaleVertex(vertices[vertexIndex + 1], center, sc);
                vertices[vertexIndex + 2] = ScaleVertex(bottomRight, center, sc);
                vertices[vertexIndex + 3] = ScaleVertex(vertices[vertexIndex + 3], center, sc);
            }
            
            text.UpdateVertexData();    
        }
        
        private Vector3 ScaleVertex(Vector3 vertex, Vector3 center, float scale)
        {
            var dir = vertex - center;
            dir *= scale;
            return center + dir;
        }
    }
}

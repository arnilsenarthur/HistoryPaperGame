using System.Collections.Generic;
using System.Globalization;
using TMPro;

namespace Game.Interface.Tags
{
    public class TypingSpeedAdvancedTextTag : AdvancedText.ClosableTag
    {
        public float speed = 1f;
        
        public override void LoadArgs(string raw, IReadOnlyDictionary<string, string> args)
        {
            speed = args.TryGetValue("speed", out var speedStr) ? float.Parse(speedStr, CultureInfo.InvariantCulture) : speed;
        }

        public override void ApplyTextEffects(AdvancedText advancedText, TMP_TextInfo text)
        {
            
        }

        public override float GetTypingSpeedMultiplier()
        {
            return speed;
        }
    }
}
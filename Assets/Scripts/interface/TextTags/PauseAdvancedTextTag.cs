using System.Collections.Generic;
using System.Globalization;

namespace Game.Interface.Tags
{
    public class PauseAdvancedTextTag : AdvancedText.Tag
    {
        public float time = 1f;
        
        public override void LoadArgs(string raw, IReadOnlyDictionary<string, string> args)
        {
            time = args.TryGetValue("time", out var timeStr) ? float.Parse(timeStr, CultureInfo.InvariantCulture) : time;
        }

        public override float GetTypingPause()
        {
            return time;
        }
    }
}
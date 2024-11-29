using System.Collections.Generic;
using System.Globalization;

namespace Game.Interface.Tags
{
    public class PauseAdvancedTextTag : AdvancedText.Tag
    {
        public float time = 1f;
        
        public override void LoadArgs(string raw, AdvancedText.Args args)
        {
            time = args.GetFloat("time", time);
        }

        public override float GetTypingPause()
        {
            return time;
        }
    }
}
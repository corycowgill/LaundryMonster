using System.Text;

namespace LaundryMonster
{
    /// <summary>
    /// The credits roll.
    ///
    /// Every tool named here actually built something in this game, and the failures
    /// listed are real ones from the build. The jokes are in the job titles, not in the
    /// facts - a credits roll that invents collaborators is just a lie with nice kerning.
    /// </summary>
    public static class Credits
    {
        /// <summary>Roughly how many lines tall the roll is, for scroll timing.</summary>
        public static int LineCount { get; private set; }

        public static string Build()
        {
            var s = new StringBuilder();

            void Head(string t) { s.Append('\n').Append(t).Append('\n'); }
            void Role(string role, string who) { s.Append(role).Append("\n    ").Append(who).Append('\n'); }
            void Note(string t) { s.Append(t).Append('\n'); }
            void Gap() { s.Append('\n'); }

            s.Append("LAUNDRY MONSTER\n");
            Note("a Hallucinated Games production");
            Gap(); Gap();

            Head("--------  THE HUMAN  --------");
            Role("CHIEF MEAT PROXY", "Cory Cowgill");
            Note("    opened the laundry room door, said \"laundry monster\",");
            Note("    and then held the clipboard for several hours");
            Gap();

            Head("--------  THE MANAGEMENT  --------");
            Role("DIRECTOR", "Claude Code");
            Role("GAME DESIGN", "Claude Code");
            Role("SYSTEMS DESIGN", "Claude Code, arguing with Claude Code");
            Role("CHIEF APOLOGIST FOR THE WRINKLE TIMER", "Claude Code");
            Gap();

            Head("--------  ENGINE ROOM  --------");
            Role("GAME ENGINE", "Unity 6, driven by Claude Code over MCP");
            Note("    no hand on the mouse; the editor was operated");
            Note("    entirely by remote control, which went about as");
            Note("    well as that sounds");
            Role("BUILD ENGINEER", "a shell script named publish-deploy.sh");
            Role("HOSTING", "Render, serving an orphan branch it has never questioned");
            Gap();

            Head("--------  GRAPHICS  --------");
            Role("LEAD TEXTURE HALLUCINATOR", "FLUX.1-schnell");
            Note("    wall paint, wicker, brushed metal, fabric, wood");
            Role("HEAD OF VOLUMETRIC GUESSWORK", "TRELLIS, image to 3D");
            Note("    hugging face space: trellis-community/TRELLIS");
            Note("    washers, dryers, the hero, the Monster, the props");
            Note("    also produced one closet that was a hollow shell,");
            Note("    and was quietly replaced by a cube");
            Role("MESH JANITOR", "Blender 5.2, headless, no GUI, no complaints");
            Note("    decimation, smoothing, UVs, FBX, and the discovery");
            Note("    that save_render() silently writes nothing at all");
            Gap();

            Head("--------  ANIMATION  --------");
            Role("MOTION CAPTURE PERFORMER", "NVIDIA Kimodo");
            Note("    kinematic motion diffusion, text straight to skeleton");
            Note("    hugging face space: nvidia/Kimodo");
            Note("    walk, idle, carry, grab, celebrate");
            Role("RIGGING", "23 SOMA bones and automatic weights");
            Role("RETARGETING", "bind-pose maths, on the fourth attempt");
            Note("    attempt one: hunched, arms folded into the chest");
            Note("    attempt two: horizontal, and somehow floating");
            Note("    attempt three: a confident theory, disproved by its own log");
            Note("    attempt four: a man walking");
            Role("SKIN WEIGHTS", "a smoothstep, after a hard cutoff shredded the shirt");
            Gap();

            Head("--------  AUDIO  --------");
            Role("COMPOSER", "Stable Audio 3");
            Note("    hugging face space: stabilityai/stable-audio-3");
            Note("    three beds: title, day, and the one that means hurry up");
            Role("LOOP SURGEON", "ffmpeg, crossfading each tail into its own head");
            Role("FOLEY", "arithmetic");
            Note("    every click, chime, buzz and alarm in this game is");
            Note("    a sine wave with opinions. no audio files were harmed");
            Note("    because no audio files were involved");
            Gap();

            Head("--------  QUALITY ASSURANCE  --------");
            Role("HEAD OF LOOKING AT IT", "rendered contact sheets");
            Note("    because \"230 baked fcurves\" and \"a man lying on his\"");
            Note("    \"side\" are the same sentence to a computer");
            Gap();

            Head("--------  IN MEMORIAM  --------");
            Note("    the shader variants, stripped from the WebGL build,");
            Note("    who took the whole room with them");
            Note("    the garments that spawned with no material at all");
            Note("    and threw an exception every 26 seconds");
            Note("    the hero, who walked backwards for one entire build");
            Note("    the socks. always the socks.");
            Gap(); Gap();

            Note("No laundry was folded during production.");
            Note("The laundry is still there.");
            Gap(); Gap();
            Note("thanks for playing");
            Gap(); Gap(); Gap();

            var text = s.ToString();
            LineCount = text.Split('\n').Length;
            return text;
        }
    }
}

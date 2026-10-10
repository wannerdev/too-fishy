using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace TooFishy
{
    public enum DialogSection
    {
        Tutorial1, Tutorial2, Tutorial3, Tutorial4, RescueCall, BossIntro, BossKillsFriend,
        FriendRescued, Win, BossDefeated, PostIntroRescue, Ak47Unlocked
    }

    /// <summary>
    /// The story dialog state of scripts/boss.gd with the texts of scripts/strings.gd. A dialog
    /// pauses the game until its last line is confirmed (boss_dialog_ui.gd).
    /// </summary>
    public static class Dialogs
    {
        public static DialogSection Section { get; private set; } = DialogSection.Tutorial1;
        public static int Index { get; private set; }
        /// <summary>boss.gd starts with boss_dialog_displayed = true, i.e. TUTORIAL1 shows at start.</summary>
        public static bool Displayed { get; private set; } = true;
        /// <summary>setDialogStage() pauses the tree; the initial TUTORIAL1 is shown without pausing.</summary>
        public static bool PausesGame => Displayed && _pausing;
        static bool _pausing;

        static readonly Dictionary<DialogSection, int> DepthMap = new()
        {
            { DialogSection.Tutorial1, 0 },
            { DialogSection.Tutorial2, 25 },
            { DialogSection.Tutorial3, 50 },
            { DialogSection.Tutorial4, 300 },
        };

        public static string From(DialogSection s) => s switch
        {
            DialogSection.BossIntro => "???",
            DialogSection.BossKillsFriend => "Blobfish",
            DialogSection.Win => "Too Fishy",
            _ => "John"
        };

        public static string CurrentFrom => From(Section);
        public static string CurrentText => ToUnityRichText(Lines[Section][Mathf.Clamp(Index, 0, Lines[Section].Length - 1)]);

        public static void Reset()
        {
            Section = DialogSection.Tutorial1;
            Index = 0;
            Displayed = true;
            _pausing = false;
        }

        public static void SetStage(DialogSection section)
        {
            Section = section;
            Index = 0;
            Displayed = true;
            _pausing = true;
        }

        /// <summary>boss.gd process_dialog_depth(): tutorial hints as the player first reaches a depth.</summary>
        public static void ProcessDepth(int maxDepthReached)
        {
            if (Section >= DialogSection.BossIntro) return;
            foreach (var kv in DepthMap)
                if (maxDepthReached >= kv.Value && Section < kv.Key)
                    SetStage(kv.Key);
        }

        /// <summary>The Continue button: next line, or close the dialog.</summary>
        public static void Continue()
        {
            if (Index < Lines[Section].Length - 1)
            {
                Index++;
                return;
            }
            if (Section == DialogSection.BossKillsFriend && GameState.Instance != null)
                GameState.Instance.Damage(GameState.Instance.Health);
            Displayed = false;
            Index = 0;
        }

        // BBCode [color=#rrggbbaa]…[/color] → Unity rich text <color=#rrggbbaa>…</color>
        static readonly Regex ColorOpen = new(@"\[color=(#[0-9a-fA-F]{6,8})\]");
        static string ToUnityRichText(string bbcode) =>
            ColorOpen.Replace(bbcode, "<color=$1>").Replace("[/color]", "</color>");

        // scripts/strings.gd boss_dialog_lines
        static readonly Dictionary<DialogSection, string[]> Lines = new()
        {
            { DialogSection.Tutorial1, new[] {
                "[color=#E0E0E0]Hey man! Is your sub ready yet?\nYes? Awesome! Go shoot some fish with that spear of yours!\n\nYou can move your submarine by using[/color] [color=#2C3E50]W A S D[/color][color=#E0E0E0], or the [/color][color=#2C3E50]Arrow Keys[/color][color=#E0E0E0]\nTo catch the fish, throw a spear with a [/color][color=#2C3E50]'Left-CLICK'[/color][color=#E0E0E0]\n\nWatch your [/color][color=#f21820ae]pressure[/color][color=#E0E0E0] or else you get damage and [/color][color=#1B5E20]Health[/color][color=#E0E0E0] levels and return to the surface when needed!\nRemember, you can swim near the dock to [/color][color=#1B5E20]upgrade your sub[/color][color=#E0E0E0] and [/color][color=#1B5E20]sell fish[/color][color=#E0E0E0].[/color]" } },
            { DialogSection.Tutorial2, new[] {
                "[color=#E0E0E0]There are some crazy fish down here! I was able to sell some of them for a lot of [/color][color=#1B5E20]money[/color][color=#E0E0E0]![/color]" } },
            { DialogSection.Tutorial3, new[] {
                "[color=#E0E0E0]I found that the [/color][color=#2C3E50]pickaxe[/color][color=#E0E0E0] is actually very useful!\nYou can break the barriers with them.\nIf you have it Press [/color][color=#2C3E50]'SPACE'[/color][color=#E0E0E0] to use it![/color]" } },
            { DialogSection.Tutorial4, new[] {
                "[color=#E0E0E0]Its awesome down here, you gotta check it out!\nThe [/color][color=#f21820ae]blobfish[/color][color=#E0E0E0] is looking at me kinda wierd... You should be careful![/color]" } },
            { DialogSection.RescueCall, new[] {
                "[color=#E0E0E0]Uhhhh- I did an oopsie, I think you need to come and get me :S[/color]" } },
            { DialogSection.BossIntro, new[] {
                "[color=#E0E0E0]Hahaha, I got your friend, looser!\nHis mind is under my control now!\nYou better not do anything stupid or he dies![/color]" } },
            { DialogSection.BossKillsFriend, new[] {
                "[color=#E0E0E0]Ohhhh, that was a mistake![/color]",
                "[color=#E0E0E0]*Splash*[/color]",
                "[color=#E0E0E0]Your friend is dead now![/color]" } },
            { DialogSection.FriendRescued, new[] {
                "[color=#E0E0E0]Oh my god thank you! That [/color][color=#f21820ae]mind controlling blobfish[/color][color=#E0E0E0]!\nYou killed it![/color]",
                "[color=#E0E0E0]I thought I was a goner![/color]",
                "[color=#E0E0E0]I will follow you to the surface, I can't wait to get out of here![/color]" } },
            { DialogSection.Win, new[] {
                "[color=#E0E0E0]You rescued your friend from the evil mind controlling blobfish![/color]",
                "[color=#E0E0E0]Congratulations, you beat our game. Feel free to keep fishing, the first game idea was created in Ludum dare 57.[/color]" } },
            { DialogSection.BossDefeated, new[] {
                "[color=#E0E0E0]The mind-controlling blobfish has been killed![/color]",
                "[color=#E0E0E0]Thank you! Come get us, we will follow your lead![/color]" } },
            { DialogSection.PostIntroRescue, new[] {
                "[color=#E0E0E0]Noo the blobfish used some crazy mind control on you. You have to rescue your friend, but first you need to get a better submarine to be able to beat the blobfish.[/color]" } },
            { DialogSection.Ak47Unlocked, new[] {
                "[color=#E0E0E0]Damn, that blobfish is no joke! [/color][color=#f21820ae]I need better weapons...[/color][color=#E0E0E0]\n\nThat's it! I heard about a [/color][color=#2C3E50]gun upgrade[/color][color=#E0E0E0] available at the dock. If I'm going to save my friend from that creature, I'll need some serious firepower.\n\nI should head back to the dock and check the [/color][color=#2C3E50]EQUIPMENT[/color][color=#E0E0E0] section for an [/color][color=#f21820ae]AK47[/color][color=#E0E0E0]. Time to show that blobfish who's boss![/color]" } },
        };
    }
}

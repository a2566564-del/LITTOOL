using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Littool.ErrorFinder
{
    internal sealed partial class ErrorFinderTab
    {
        internal enum ChipKind
        {
            Severe = 0,
            Gray = 1,
            Confirmed = 2,
            Similar = 3,
            Plain = 4,
        }

        sealed class Styles
        {
            readonly bool m_ProSkin;
            readonly List<Texture2D> m_Textures = new List<Texture2D>();

            public readonly GUIStyle Heading;
            public readonly GUIStyle Summary;
            public readonly GUIStyle SummaryTime;
            public readonly GUIStyle Body;
            public readonly GUIStyle BodyBold;
            public readonly GUIStyle Small;
            public readonly GUIStyle SmallDim;
            public readonly GUIStyle SmallDimRight;
            public readonly GUIStyle Section;
            public readonly GUIStyle CardTitle;
            public readonly GUIStyle Card;
            public readonly GUIStyle Caution;
            public readonly GUIStyle Note;
            public readonly GUIStyle Code;
            public readonly GUIStyle CodeRich;
            public readonly GUIStyle Raw;
            public readonly GUIStyle StepNumber;
            public readonly GUIStyle Chip;
            public readonly GUIStyle FilterChip;
            public readonly GUIStyle Link;
            public readonly GUIStyle Foldout;
            public readonly GUIStyle IdleTitle;
            public readonly GUIStyle Count;
            public readonly GUIStyle MiniButton;
            public readonly GUIStyle Path;

            public readonly Color Edge;
            public readonly Color CardEdge;
            public readonly Color Accent;
            public readonly Color OkColor;
            public readonly Color StepBack;
            public readonly Color Hover;
            public readonly Color[] ChipBack = new Color[5];
            public readonly Color[] ChipText = new Color[5];
            public readonly Color FilterOn;
            public readonly Color FilterOff;
            public readonly Color FilterOnText;
            public readonly Color SevereBar;
            public readonly Color SimilarBar;
            public readonly Color GrayBar;
            public readonly string MarkHex;

            public bool IsValid => m_ProSkin == EditorGUIUtility.isProSkin && Card.normal.background != null;

            public Styles()
            {
                m_ProSkin = EditorGUIUtility.isProSkin;
                bool pro = m_ProSkin;
                Color text = pro ? new Color32(0xD2, 0xD2, 0xD2, 0xFF) : new Color32(0x20, 0x20, 0x20, 0xFF);
                Color dim = pro ? new Color32(0xA0, 0xA0, 0xA0, 0xFF) : new Color32(0x58, 0x58, 0x58, 0xFF);
                Accent = pro ? new Color32(0x7B, 0xAE, 0xFA, 0xFF) : new Color32(0x1F, 0x5F, 0xBF, 0xFF);
                OkColor = pro ? new Color32(0x8F, 0xC9, 0x8F, 0xFF) : new Color32(0x2E, 0x7D, 0x32, 0xFF);
                Edge = pro ? new Color32(0x23, 0x23, 0x23, 0xFF) : new Color32(0x99, 0x99, 0x99, 0xFF);
                CardEdge = pro ? new Color32(0x24, 0x24, 0x24, 0xFF) : new Color32(0xA6, 0xA6, 0xA6, 0xFF);
                StepBack = pro ? new Color32(0x4A, 0x4A, 0x4A, 0xFF) : new Color32(0xB8, 0xB8, 0xB8, 0xFF);
                Hover = pro ? new Color(1f, 1f, 1f, 0.05f) : new Color(0f, 0f, 0f, 0.05f);
                MarkHex = pro ? "#F2C14E" : "#8A5A00";

                // chips: severe (red), gray, confirmed (green), similar (blue), plain
                ChipBack[0] = pro ? new Color32(0x5C, 0x2B, 0x2B, 0xFF) : new Color32(0xF2, 0xCB, 0xCB, 0xFF);
                ChipText[0] = pro ? new Color32(0xF2, 0xB8, 0xB8, 0xFF) : new Color32(0x8A, 0x1F, 0x1F, 0xFF);
                ChipBack[1] = pro ? new Color32(0x47, 0x47, 0x47, 0xFF) : new Color32(0xD0, 0xD0, 0xD0, 0xFF);
                ChipText[1] = pro ? new Color32(0xC8, 0xC8, 0xC8, 0xFF) : new Color32(0x30, 0x30, 0x30, 0xFF);
                ChipBack[2] = pro ? new Color32(0x2C, 0x4A, 0x2E, 0xFF) : new Color32(0xCD, 0xE8, 0xCE, 0xFF);
                ChipText[2] = pro ? new Color32(0xA8, 0xDA, 0xA8, 0xFF) : new Color32(0x1E, 0x5E, 0x22, 0xFF);
                ChipBack[3] = pro ? new Color32(0x2A, 0x3E, 0x5C, 0xFF) : new Color32(0xCF, 0xE0, 0xF7, 0xFF);
                ChipText[3] = pro ? new Color32(0xAC, 0xCB, 0xF2, 0xFF) : new Color32(0x1F, 0x4F, 0x8F, 0xFF);
                ChipBack[4] = new Color(0f, 0f, 0f, 0f);
                ChipText[4] = dim;
                SevereBar = pro ? new Color32(0xC0, 0x4B, 0x4B, 0xFF) : new Color32(0xC6, 0x3A, 0x3A, 0xFF);
                SimilarBar = pro ? new Color32(0x4F, 0x86, 0xD6, 0xFF) : new Color32(0x3A, 0x72, 0xC6, 0xFF);
                GrayBar = pro ? new Color32(0x6A, 0x6A, 0x6A, 0xFF) : new Color32(0x8A, 0x8A, 0x8A, 0xFF);
                FilterOn = pro ? new Color32(0x3D, 0x5F, 0x8F, 0xFF) : new Color32(0x3A, 0x72, 0xC6, 0xFF);
                FilterOff = pro ? new Color32(0x45, 0x45, 0x45, 0xFF) : new Color32(0xD6, 0xD6, 0xD6, 0xFF);
                FilterOnText = Color.white;

                Heading = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
                Summary = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, wordWrap = true };
                SummaryTime = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
                SummaryTime.normal.textColor = dim;
                Body = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = false, fontSize = 11 };
                BodyBold = new GUIStyle(Body) { fontStyle = FontStyle.Bold };
                Small = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
                Small.normal.textColor = text;
                SmallDim = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
                SmallDim.normal.textColor = dim;
                SmallDimRight = new GUIStyle(SmallDim) { alignment = TextAnchor.MiddleRight, wordWrap = false };
                Section = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
                Section.normal.textColor = dim;
                CardTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, wordWrap = true };
                Count = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
                Count.normal.textColor = dim;

                Color cardBack = pro ? new Color32(0x30, 0x30, 0x30, 0xFF) : new Color32(0xDE, 0xDE, 0xDE, 0xFF);
                Card = new GUIStyle { padding = new RectOffset(12, 10, 8, 10), margin = new RectOffset(0, 0, 0, 0) };
                Card.normal.background = Solid(cardBack);

                Color cautionBack = pro ? new Color32(0x4A, 0x40, 0x24, 0xFF) : new Color32(0xFB, 0xEF, 0xC8, 0xFF);
                Color cautionText = pro ? new Color32(0xEC, 0xDB, 0xA6, 0xFF) : new Color32(0x5E, 0x48, 0x00, 0xFF);
                Caution = new GUIStyle(EditorStyles.label) { wordWrap = true, fontSize = 11, padding = new RectOffset(8, 8, 5, 6), margin = new RectOffset(0, 0, 2, 2) };
                Caution.normal.background = Solid(cautionBack);
                Caution.normal.textColor = cautionText;

                Color noteBack = pro ? new Color32(0x25, 0x36, 0x4C, 0xFF) : new Color32(0xDC, 0xEA, 0xFB, 0xFF);
                Color noteText = pro ? new Color32(0xB9, 0xD2, 0xF2, 0xFF) : new Color32(0x1C, 0x3C, 0x6C, 0xFF);
                Note = new GUIStyle(Caution);
                Note.normal.background = Solid(noteBack);
                Note.normal.textColor = noteText;

                Color codeBack = pro ? new Color32(0x26, 0x26, 0x26, 0xFF) : new Color32(0xEE, 0xEE, 0xEE, 0xFF);
                Code = new GUIStyle(EditorStyles.label) { wordWrap = true, fontSize = 10, padding = new RectOffset(6, 6, 4, 5), margin = new RectOffset(0, 0, 2, 2) };
                Code.normal.background = Solid(codeBack);
                Code.normal.textColor = text;
                CodeRich = new GUIStyle(Code) { richText = true };
                Raw = new GUIStyle(EditorStyles.label) { wordWrap = true, fontSize = 10, richText = false, padding = new RectOffset(6, 6, 4, 4) };
                Raw.normal.background = Solid(codeBack);
                Raw.normal.textColor = text;
                Raw.focused.background = Raw.normal.background;
                Raw.focused.textColor = text;

                StepNumber = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 10 };
                StepNumber.normal.textColor = pro ? new Color32(0xE6, 0xE6, 0xE6, 0xFF) : new Color32(0x20, 0x20, 0x20, 0xFF);
                Chip = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(7, 7, 0, 0), fontSize = 10 };
                FilterChip = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(9, 9, 0, 0), fontSize = 11 };
                Link = new GUIStyle(EditorStyles.miniLabel) { wordWrap = false, clipping = TextClipping.Clip };
                Link.normal.textColor = Accent;
                Link.hover.textColor = Accent;
                Foldout = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold, fontSize = 11 };
                Foldout.normal.textColor = dim;
                Foldout.onNormal.textColor = dim;
                IdleTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, wordWrap = true };
                MiniButton = new GUIStyle(EditorStyles.miniButton) { fixedHeight = 18f, fontSize = 10 };
                Path = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
                Path.normal.textColor = text;
            }

            Texture2D Solid(Color c)
            {
                var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                t.SetPixel(0, 0, c);
                t.Apply();
                m_Textures.Add(t);
                return t;
            }

            public void Dispose()
            {
                foreach (var t in m_Textures)
                {
                    if (t != null) Object.DestroyImmediate(t);
                }
                m_Textures.Clear();
            }
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    // Selectable owns pointer and keyboard transitions. The shared skin also dims the caption when disabled.
    internal sealed class NativeHudButton : Button
    {
        Sprite readySprite,disabledSprite;
        Color readyText,disabledText;
        Image background;
        internal HudTone Tone {get;private set;}
        internal Text Caption {get;private set;}

        internal void SetTheme(Image image,Text caption,Sprite ready,Sprite disabled,Color ink,Color muted,HudTone tone)
        {
            background=image;
            Caption=caption;
            readySprite=ready;
            disabledSprite=disabled;
            readyText=ink;
            disabledText=muted;
            Tone=tone;
            UpdateSkin(!IsInteractable());
        }

        protected override void DoStateTransition(SelectionState state,bool instant)
        {
            UpdateSkin(state==SelectionState.Disabled);
            base.DoStateTransition(state,instant);
        }

        void UpdateSkin(bool disabled)
        {
            if(!background||!Caption)return;
            var sprite=disabled?disabledSprite:readySprite;
            var color=disabled?disabledText:readyText;
            if(background.sprite!=sprite)background.sprite=sprite;
            if(Caption.color!=color)Caption.color=color;
        }
    }
}

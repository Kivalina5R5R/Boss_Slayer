using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Deepspace
{
    public sealed class BossVictoryPanel:MonoBehaviour
    {
        public BossPassiveState.Kind BossKind{get;private set;}
        public string BossName{get;private set;}
        TMP_Text nameLabel,progress,countdown,title,caption;Image backdrop;BossEmblemGraphic emblem;BossVictoryGraphic celebration;CanvasGroup group;
        float age;bool next;
        public const float Duration=6f;
        static RectTransform Area(Transform parent,string name,Vector2 min,Vector2 max)
        {
            var go=new GameObject(name,typeof(RectTransform));go.layer=parent.gameObject.layer;var rect=(RectTransform)go.transform;rect.SetParent(parent,false);rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
        }
        static TMP_Text Text(Transform parent,string name,TMP_FontAsset font,string value,Vector2 min,Vector2 max,float size,Color color)
        {
            var text=Area(parent,name,min,max).gameObject.AddComponent<TextMeshProUGUI>();if(font!=null)text.font=font;
            text.text=value;text.fontStyle=FontStyles.Bold;text.alignment=TextAlignmentOptions.Center;text.enableAutoSizing=true;text.fontSizeMin=18;text.fontSizeMax=size;
            text.textWrappingMode=TextWrappingModes.NoWrap;text.color=color;text.raycastTarget=false;return text;
        }
        public static BossVictoryPanel Create(RectTransform parent,TMP_FontAsset font,bool floor)
        {
            var root=Area(parent,"Panel == Boss Victory",Vector2.zero,Vector2.one);var panel=root.gameObject.AddComponent<BossVictoryPanel>();
            panel.backdrop=root.gameObject.AddComponent<Image>();panel.backdrop.color=BackdropColor;
            panel.group=root.gameObject.AddComponent<CanvasGroup>();
            var decoration=Area(root,"Victory voxel celebration",Vector2.zero,Vector2.one);panel.celebration=decoration.gameObject.AddComponent<BossVictoryGraphic>();panel.celebration.raycastTarget=false;panel.celebration.Floor=floor;
            var holder=Area(root,"Defeated boss medallion",floor?new Vector2(.32f,.33f):new Vector2(.22f,.2f),floor?new Vector2(.68f,.70f):new Vector2(.42f,.8f));
            var icon=Area(holder,"Boss emblem",Vector2.zero,Vector2.one);panel.emblem=icon.gameObject.AddComponent<BossEmblemGraphic>();panel.emblem.raycastTarget=false;
            var fit=icon.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=1;
            float left=floor?.1f:.46f,right=floor?.9f:.86f;
            panel.title=Text(root,"Victory title",font,"VICTORY",new Vector2(left,floor?.83f:.54f),new Vector2(right,floor?.97f:.735f),floor?156:340,new Color(1,.85f,.36f));
            // บาส 2026-09-29 playtest: the wall's secondary lines (85/72 on a 2160-tall wall) were unreadable from the
            // floor. Wall boxes regrown so caption/progress/countdown are 140/120/120 (~6% of wall height) with
            // letter-spaced caps; the floor keeps its sizes (it's read up close).
            panel.caption=Text(root,"Defeat caption",font,"BOSS DEFEATED",new Vector2(left,floor?.74f:.75f),new Vector2(right,floor?.81f:.87f),floor?42:140,new Color(.65f,.77f,.9f));
            panel.nameLabel=Text(root,"Defeated boss name",font,"",new Vector2(left,floor?.235f:.345f),new Vector2(right,floor?.325f:.525f),floor?86:200,Color.white);
            panel.progress=Text(root,"Session progress",font,"",new Vector2(left,floor?.16f:.225f),new Vector2(right,floor?.22f:.325f),floor?36:120,new Color(.6f,.73f,.86f));
            panel.countdown=Text(root,"Next screen countdown",font,"",new Vector2(left,floor?.055f:.10f),new Vector2(right,floor?.12f:.20f),floor?36:120,new Color(.8f,.9f,1));
            if(!floor)foreach(var line in new[]{panel.caption,panel.progress,panel.countdown}){line.characterSpacing=10;line.fontSizeMin=line.fontSizeMax*.6f;line.outlineWidth=.15f;line.outlineColor=new Color32(0,0,0,220);}
            root.gameObject.SetActive(false);return panel;
        }
        static readonly Color BackdropColor=new Color(.008f,.016f,.04f,.985f),TitleColor=new Color(1,.85f,.36f),CaptionColor=new Color(.65f,.77f,.9f);
        // บาส 2026-09-29: per-boss theme. VICTORY keeps its gold (celebration) and only picks up a warm tint of the
        // boss text colour; backdrop/caption/name/celebration voxels wear the defeated boss. strength 0 = old look.
        public float ThemeStrength;public bool ThemePhase2;
        public void Show(string modelName,int round,int total,bool hasNext,Color accent,bool escaped=false)
        {
            var theme=BossThemePalette.UiTheme(BossPassiveState.Identify(modelName),ThemePhase2);float k=Mathf.Clamp01(ThemeStrength);
            var back=Color.Lerp(BackdropColor,theme.panelDark*new Color(.55f,.55f,.55f,1),k);back.a=BackdropColor.a;backdrop.color=back;
            title.color=Color.Lerp(TitleColor,Color.Lerp(TitleColor,theme.textLight,.3f),k);caption.color=Color.Lerp(CaptionColor,Color.Lerp(theme.secondary,Color.white,.55f),k);
            accent=Color.Lerp(accent,theme.glow,k);
            BossKind=BossPassiveState.Identify(modelName);BossName=BossNextBossCard.NameFor(BossKind);next=hasNext;
            nameLabel.text=BossName;nameLabel.color=Color.Lerp(Color.Lerp(accent,Color.white,.45f),theme.textLight,k);
            emblem.Kind=BossKind;emblem.SetVerticesDirty();celebration.Accent=accent;celebration.Sample(0);
            caption.text=escaped?"BOSS ESCAPED":"BOSS DEFEATED";
            progress.text=total<=0?"FINAL BOSS  CLEARED":"ROUND "+Mathf.Max(1,round)+" / "+total+(escaped?"  ESCAPED":"  CLEARED"); // total 0 = the rush's Abyssal King
            age=0;gameObject.SetActive(true);transform.SetAsLastSibling();Sample(0);
            ApplyFantasy();
        }
        // Arcane-codex finish: carved-gold VICTORY, parchment secondary lines (they were ice blue), gold rules either side
        // of the title. The boss-name tint and celebration voxels keep the defeated boss's colour.
        void ApplyFantasy()
        {
            var font=title.font;
            BossFantasyTheme.GoldText(title,font,title.fontSizeMax>200?20:12,.12f);
            caption.color=BossFantasyTheme.ParchmentDim;progress.color=BossFantasyTheme.Gold;countdown.color=BossFantasyTheme.Parchment;
            var root=(RectTransform)transform;var t=title.rectTransform;float y=(t.anchorMin.y+t.anchorMax.y)*.5f,u=Mathf.Max(1,root.rect.height)/(title.fontSizeMax>200?2160f:(float)FloorDisplaySettings.Height);
            float mid=(t.anchorMin.x+t.anchorMax.x)*.5f,half=title.fontSizeMax>200?.12f:.16f;
            BossFantasyTheme.Rule(root,"Title Rule Left",Mathf.Max(.02f,mid-half-.12f),mid-half,y,4*u,true);
            BossFantasyTheme.Rule(root,"Title Rule Right",mid+half,Mathf.Min(.98f,mid+half+.12f),y,4*u,false);
        }
        public void Sample(float elapsed)
        {
            age=elapsed;group.alpha=Mathf.SmoothStep(0,1,Mathf.Clamp01(age/.35f))*Mathf.Clamp01((Duration-age)/.3f);
            countdown.text=(next?"BOSS SELECT IN ":"RESULTS IN ")+Mathf.CeilToInt(Mathf.Max(0,Duration-age))+"s";
        }
        public void Hide(){gameObject.SetActive(false);}
    }
}

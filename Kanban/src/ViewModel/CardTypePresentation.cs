using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using static Kanban.Util.Util;
using Kanban.Util;
using Kanban.Repository;

namespace Kanban.ViewModel
{
    struct CardTypeView
    {
        public CardTypeView(CardType cardType)
        {
            switch(cardType)
            {
                case CardType.Feature:
                    BackgroundColor = Colors.Green;
                    CardTypeIconUri = "feature.png";
                    break;
                case CardType.Improvement:
                    BackgroundColor = Colors.Lime;
                    CardTypeIconUri = "improvement.png";
                    break;
                case CardType.BugFix:
                    BackgroundColor = Colors.Red;
                    CardTypeIconUri = "bug.png";
                    break;
                case CardType.DefectInvestigation:
                    BackgroundColor = Colors.LightPink;
                    CardTypeIconUri = "defect_investigation.png";
                    break;
                case CardType.TechnicalDebt:
                    BackgroundColor = Colors.Yellow;
                    CardTypeIconUri = "clean_up.png";
                    break;
                case CardType.Cancelled:
                    BackgroundColor = Colors.DarkGray;
                    CardTypeIconUri = "cancel.png";
                    break;
                case CardType.Evaluation:
                    BackgroundColor = Colors.PapayaWhip;
                    CardTypeIconUri = "evaluation.png";
                    break;
                case CardType.TestDesign:
                    BackgroundColor = Colors.Orange;
                    CardTypeIconUri = "test_design.png";
                    break;
                case CardType.Task:
                    BackgroundColor = Colors.MintCream;
                    CardTypeIconUri = "task.png";
                    break;
                default:
                    BackgroundColor = Colors.Green;
                    CardTypeIconUri = "feature.png";
                    break;
            }
            CardTypeToolTip = EnumUtil.GetEnumDescription(cardType);
        }

        public Color BackgroundColor { get; private set; }
        public string CardTypeIconUri { get; private set; }
        public string CardTypeToolTip { get; private set; }
    }

    static class CardTypePresentation
    {
        static Dictionary<CardType, CardTypeView> CardTypeViews;
        static CardTypePresentation()
        {
            CardTypeViews = new Dictionary<CardType, CardTypeView>
                    {
                        { CardType.Feature, new CardTypeView(CardType.Feature) },
                        { CardType.Improvement, new CardTypeView(CardType.Improvement) },
                        { CardType.BugFix, new CardTypeView(CardType.BugFix) },
                        { CardType.DefectInvestigation, new CardTypeView(CardType.DefectInvestigation) },
                        { CardType.TechnicalDebt, new CardTypeView(CardType.TechnicalDebt) },
                        { CardType.Cancelled, new CardTypeView(CardType.Cancelled) },
                        { CardType.Evaluation, new CardTypeView(CardType.Evaluation) },
                        { CardType.TestDesign, new CardTypeView(CardType.TestDesign) },
                        { CardType.Task, new CardTypeView(CardType.Task) },
                    };
        }

        public static Color CardBackgroundColor(CardType cardType) => CardTypeViews[cardType].BackgroundColor;
        public static string CardTypeIconUri(CardType cardType) => PackImageURI(CardTypeViews[cardType].CardTypeIconUri);
        public static string CardTypeToolTip(CardType cardType) => CardTypeViews[cardType].CardTypeToolTip;
    }
}

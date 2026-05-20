using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;

namespace Kanban.Infrastructure
{
    public class CardFactory : Model.ICardFactory
    {
        private Repository.IDBCard m_DBCards;
        private Repository.IDBActivity m_DBActivity;
        private Util.ILocalization m_Localization;

        public CardFactory(Repository.IDBCard dbCards, Repository.IDBActivity dbActivity, Util.ILocalization localization)
        {
            m_DBCards = dbCards;
            m_DBActivity = dbActivity;
            m_Localization = localization;
        }

        public async Task<Model.Card> CreateCard(ObjectId boardID, Model.WorkState workState)
        {
            Model.Card card = new Model.Card(boardID, workState, m_Localization.CardDefaultTitle);
            await m_DBCards.Insert(card.CardOfRepository);
            card.WorkState = await CreateNewActivities(boardID, card, workState);

            return card;
        }

        private async Task<Model.WorkState> CreateNewActivities(ObjectId boardID, Model.Card card, Model.WorkState workState)
        {
            Model.WorkState newWorkState = new Model.WorkState { ProcessStepSeqNo = workState.ProcessStepSeqNo, IsWIP = workState.IsWIP, IsBlocked = workState.IsBlocked, IsMergedIntoMaster = workState.IsMergedIntoMaster, IsMergedIntoMajorBranch = workState.IsMergedIntoMajorBranch };
            Model.Activity newActivity = new Model.Activity { BoardID = boardID, CardID = card._id, WorkState = newWorkState, StateChangedDate = DateTime.Now };
            await m_DBActivity.Insert(newActivity.ActivityOfRepository);

            return newWorkState;
        }
    }
}

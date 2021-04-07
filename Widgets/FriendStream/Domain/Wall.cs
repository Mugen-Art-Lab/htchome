using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Social.Base;

namespace FriendStream.Domain
{
    public class Wall
    {
        public List<FriendStreamEntry> CurrentEntries;
        private DateTime lastEntryDate;

        public delegate void RefreshFinishedHandler(IEnumerable<FriendStreamEntry> newItems);
        public event RefreshFinishedHandler RefreshFinished;

        public delegate void EntryUpdatedHandler(FriendStreamEntry entry);
        public event EntryUpdatedHandler EntryUpdated;

        //sign in to all providers
        public void Initialize()
        {
            CurrentEntries = new List<FriendStreamEntry>();
            foreach (var p in App.ProviderManager.Providers)
            {
                if (p.IsActive)
                {
                    p.SignIn();
                }
            }
        }

        public void Refresh()
        {
            var prevEntries = CurrentEntries.Select(item => (FriendStreamEntry)item.Clone()).ToList();
            CurrentEntries.Clear();
            foreach (var p in App.ProviderManager.Providers)
            {
                if (!p.IsActive) continue;
                if (!p.Signed)
                    p.SignIn();
                var entries = p.GetFriendStream();
                CurrentEntries.AddRange(entries);
            }

            if (CurrentEntries.Count == 0)
                return;
            CurrentEntries = CurrentEntries.OrderByDescending(x => x.CreatedTime).ToList();
            CurrentEntries.Reverse();
            if (prevEntries.Count > 0)
            {
                lastEntryDate = prevEntries[prevEntries.Count - 1].CreatedTime;

                foreach (var entry in prevEntries)
                {
                    var curEntry = FindEntryById(entry.Id);
                    if (curEntry == null)
                        continue;
                    if ((entry.Comments != curEntry.Comments || entry.Likes != curEntry.Likes) && EntryUpdated != null)
                    {
                        EntryUpdated(curEntry);
                    }
                }
            }
            var newEntries = CurrentEntries.FindAll(x => x.CreatedTime.CompareTo(lastEntryDate) == 1);
            if (RefreshFinished != null)
                RefreshFinished(newEntries);
        }

        private FriendStreamEntry FindEntryById(string id)
        {
            return CurrentEntries.Find(x => x.Id == id);
        }

        public void Clear()
        {
            lastEntryDate = new DateTime(0);
            CurrentEntries.Clear();
        }
    }
}

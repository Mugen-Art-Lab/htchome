using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Social.Base
{
    public class FriendStreamEntry :ICloneable
    {
        public string Provider;
        public string Id;
        public string FromId;
        public string FromName;
        public string Message;
        public FriendStreamEntryType Type;
        public int Likes;
        public int Comments;
        public string UserPic;
        public DateTime CreatedTime;
        public DateTime UpdatedTime;
        public string Url;
        //for links
        public string Name;
        public string Caption;
        public string Description;
        public string Picture;

        public object Clone()
        {
            var clone = new FriendStreamEntry();
            clone.Id = Id;
            clone.FromId = FromId;
            clone.FromName = FromName;
            clone.Message = Message;
            clone.Type = Type;
            clone.Likes = Likes;
            clone.Comments = Comments;
            clone.UserPic = UserPic;
            clone.CreatedTime = CreatedTime;
            clone.UpdatedTime = UpdatedTime;
            clone.Name = Name;
            clone.Caption = Caption;
            clone.Description = Description;
            clone.Picture = Picture;
            return clone;
        }
    }
}

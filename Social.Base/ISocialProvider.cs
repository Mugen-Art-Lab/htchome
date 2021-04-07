using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Social.Base
{
    public interface ISocialProvider
    {
        string GetName();
        void SignIn();
        List<FriendStreamEntry> GetFriendStream();
        void Post(string status);

        event EventHandler SignedIn;
    }
}

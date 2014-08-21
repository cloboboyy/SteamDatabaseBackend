﻿/*
 * Copyright (c) 2013-2015, SteamDB. All rights reserved.
 * Use of this source code is governed by a BSD-style license that can be
 * found in the LICENSE file.
 */
using System;
using MySql.Data.MySqlClient;
using SteamKit2;

namespace SteamDatabaseBackend
{
    class Steam
    {
        private static Steam _instance = new Steam();
        public static Steam Instance { get { return _instance; } }

        public SteamClient Client { get; private set; }
        public SteamUser User { get; private set; }
        public SteamApps Apps { get; private set; }
        public SteamFriends Friends { get; private set; }
        public SteamUserStats UserStats { get; private set; }
        public CallbackManager CallbackManager { get; private set; }

        public PICSChanges PICSChanges { get; private set; }
        public DepotProcessor DepotProcessor { get; private set; }

        public bool IsRunning { get; set; }

        public Steam()
        {
            Client = new SteamClient();

            User = Client.GetHandler<SteamUser>();
            Apps = Client.GetHandler<SteamApps>();
            Friends = Client.GetHandler<SteamFriends>();
            UserStats = Client.GetHandler<SteamUserStats>();

            CallbackManager = new CallbackManager(Client);

            Client.AddHandler(new FreeLicense());

            new Connection(CallbackManager);
            new PICSProductInfo(CallbackManager);
            new PICSTokens(CallbackManager);

            if (!Settings.IsFullRun)
            {
                new AccountInfo(CallbackManager);
                new MarketingMessage(CallbackManager);
                new ClanState(CallbackManager);
                new ChatMemberInfo(CallbackManager);
            }

            PICSChanges = new PICSChanges(CallbackManager);
            DepotProcessor = new DepotProcessor(Client, CallbackManager);

            IsRunning = true;
        }

        public void Tick()
        {
            Client.Connect();

            while (IsRunning)
            {
                CallbackManager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
            }
        }

        public static string GetPackageName(uint subID, bool returnEmptyOnFailure = false)
        {
            using (MySqlDataReader Reader = DbWorker.ExecuteReader("SELECT `Name`, `StoreName` FROM `Subs` WHERE `SubID` = @SubID", new MySqlParameter("SubID", subID)))
            {
                if (Reader.Read())
                {
                    string name = DbWorker.GetString("Name", Reader);

                    if (name.StartsWith("Steam Sub", StringComparison.Ordinal))
                    {
                        string nameStore = DbWorker.GetString("StoreName", Reader);

                        if (!string.IsNullOrEmpty(nameStore))
                        {
                            name = string.Format("{0} {1}({2}){3}", name, Colors.DARKGRAY, nameStore, Colors.NORMAL);
                        }
                    }

                    return name;
                }
            }

            return returnEmptyOnFailure ? string.Empty : string.Format("SubID {0}", subID);
        }

        public static string GetAppName(uint appID, bool returnEmptyOnFailure = false)
        {
            using (MySqlDataReader reader = DbWorker.ExecuteReader("SELECT `Name`, `LastKnownName` FROM `Apps` WHERE `AppID` = @AppID", new MySqlParameter("AppID", appID)))
            {
                if (reader.Read())
                {
                    string name = DbWorker.GetString("Name", reader);
                    string nameLast = DbWorker.GetString("LastKnownName", reader);

                    if (!string.IsNullOrEmpty(nameLast) && !name.Equals(nameLast))
                    {
                        return string.Format("{0} {1}({2}){3}", name, Colors.DARKGRAY, nameLast, Colors.NORMAL);
                    }

                    return name;
                }
            }

            return returnEmptyOnFailure ? string.Empty : string.Format("AppID {0}", appID);
        }
    }
}
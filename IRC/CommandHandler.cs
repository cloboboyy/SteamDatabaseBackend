﻿/*
 * Copyright (c) 2013-2015, SteamDB. All rights reserved.
 * Use of this source code is governed by a BSD-style license that can be
 * found in the LICENSE file.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NetIrc2.Events;
using SteamKit2;

namespace SteamDatabaseBackend
{
    class CommandHandler
    {
        private readonly List<Command> RegisteredCommands;
        private readonly PubFileCommand PubFileHandler;
        private readonly LinkExpander LinkExpander;

        public CommandHandler()
        {
            LinkExpander = new LinkExpander();
            PubFileHandler = new PubFileCommand();

            RegisteredCommands = new List<Command>
            {
                new BlogCommand(),
                new PlayersCommand(),
                new AppCommand(),
                new PackageCommand(),
                new SteamIDCommand(),
                new GIDCommand(),
                PubFileHandler,
                new UGCCommand(),
                new EnumCommand(),
                new ServersCommand(),
                new BinariesCommand(),
                new ImportantCommand(),
                new ReloginCommand(),
            };

            // Register help command last so we can pass the list of the commands
            RegisteredCommands.Add(new HelpCommand(RegisteredCommands));

            Log.WriteInfo("CommandHandler", "Registered {0} commands", RegisteredCommands.Count);
        }

        public void OnIRCMessage(object sender, ChatMessageEventArgs e)
        {
            var commandData = new CommandArguments
            {
                CommandType = ECommandType.IRC,
                SenderIdentity = e.Sender,
                Recipient = e.Recipient,
                Message = e.Message
            };

            if (Steam.Instance.Client.IsConnected)
            {
                PubFileHandler.OnMessage(commandData);
            }

            LinkExpander.OnMessage(commandData);

            if (e.Message[0] != Settings.Current.IRC.CommandPrefix)
            {
                return;
            }

            var message = (string)e.Message;
            var messageArray = message.Split(' ');
            var trigger = messageArray[0];

            if (trigger.Length < 2)
            {
                return;
            }
                
            trigger = trigger.Substring(1);

            var command = RegisteredCommands.FirstOrDefault(cmd => cmd.Trigger.Equals(trigger));

            if (command == null)
            {
                return;
            }

            commandData.Message = message.Substring(messageArray[0].Length).Trim();

            if (command.IsSteamCommand && !Steam.Instance.Client.IsConnected)
            {
                commandData.Reply("Not connected to Steam.");

                return;
            }

            if (command.IsAdminCommand)
            {
                var ident = string.Format("{0}@{1}", e.Sender.Username, e.Sender.Hostname);

                if (!Settings.Current.IRC.Admins.Contains(ident))
                {
                    return;
                }
            }

            Log.WriteInfo("CommandHandler", "Handling IRC command \"{0}\" for {1}", message, commandData);

            TryCommand(command, commandData);
        }

        public void OnSteamFriendMessage(SteamFriends.FriendMsgCallback callback)
        {
            if (callback.EntryType != EChatEntryType.ChatMsg        // Is chat message
            ||  callback.Sender == Steam.Instance.Client.SteamID    // Is not sent by the bot
            ||  callback.Message[0] != Settings.Current.IRC.CommandPrefix
            ||  callback.Message.Contains('\n')                     // Does not contain new lines
            )
            {
                return;
            }

            var commandData = new CommandArguments
            {
                CommandType = ECommandType.SteamIndividual,
                SenderID = callback.Sender,
                Message = callback.Message
            };

            Log.WriteInfo("CommandHandler", "Handling Steam friend command \"{0}\" for {1}", callback.Message, commandData);

            HandleSteamMessage(commandData);
        }

        public void OnSteamChatMessage(SteamFriends.ChatMsgCallback callback)
        {
            if (callback.ChatMsgType != EChatEntryType.ChatMsg || callback.ChatterID == Steam.Instance.Client.SteamID)
            {
                return;
            }

            var commandData = new CommandArguments
            {
                CommandType = ECommandType.SteamChatRoom,
                SenderID = callback.ChatterID,
                ChatRoomID = callback.ChatRoomID,
                Message = callback.Message
            };

            PubFileHandler.OnMessage(commandData);
            LinkExpander.OnMessage(commandData);

            if (callback.Message[0] != Settings.Current.IRC.CommandPrefix || callback.Message.Contains('\n'))
            {
                return;
            }

            Log.WriteInfo("CommandHandler", "Handling Steam command \"{0}\" for {1}", callback.Message, commandData);

            HandleSteamMessage(commandData);
        }

        private void HandleSteamMessage(CommandArguments commandData)
        {
            var message = commandData.Message;
            var i = message.IndexOf(' ');
            var inputCommand = i == -1 ? message.Substring(1) : message.Substring(1, i - 1);

            var command = RegisteredCommands.FirstOrDefault(cmd => cmd.Trigger.Equals(inputCommand));

            if (command == null)
            {
                return;
            }

            commandData.Message = i == -1 ? string.Empty : message.Substring(i).Trim();

            if (command.IsAdminCommand && !Settings.Current.SteamAdmins.Contains(commandData.SenderID.ConvertToUInt64()))
            {
                commandData.Reply("You're not an admin!");

                return;
            }

            TryCommand(command, commandData);
        }

        private static async void TryCommand(Command command, CommandArguments commandData)
        {
            try
            {
                await command.OnCommand(commandData);
            }
            catch (TaskCanceledException)
            {
                commandData.Reply("Your command timed out.");
            }
            catch (AsyncJobFailedException)
            {
                commandData.Reply("Steam says this job failed, unable to execute your command.");
            }
            catch (Exception e)
            {
                Log.WriteError("CommandHandler", "Exception while executing a command: {0}\n{1}", e.Message, e.StackTrace);

                commandData.Reply("Exception: {0}", e.Message);

                ErrorReporter.Notify(e);
            }
        }
    }
}

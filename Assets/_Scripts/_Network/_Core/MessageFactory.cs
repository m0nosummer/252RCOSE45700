using System;
using System.Collections.Generic;
using Arena.Core;
using Arena.Network.Messages;
using UnityEngine;

namespace Arena.Network
{
    public static class MessageFactory
    {
        private static readonly Dictionary<MessageType, Func<INetworkMessage>> messageCreators 
            = new Dictionary<MessageType, Func<INetworkMessage>>
        {
            { MessageType.PlayerInput, () => new PlayerInputMessage() },
            { MessageType.PlayerState, () => new PlayerStateMessage() },
            { MessageType.Handshake, () => new HandshakeMessage() },
            { MessageType.Fire, () => new FireMessage() },
            { MessageType.BulletSpawn, () => new BulletSpawnMessage() },
            { MessageType.BulletDestroy, () => new BulletDestroyMessage() },
            { MessageType.PlayerJoined, () => new PlayerJoinedMessage() },
            { MessageType.PlayerLeft, () => new PlayerLeftMessage() },
        };
        
        public static INetworkMessage CreateMessage(byte[] buffer, int length)
        {
            if (buffer == null || length < 1)
            {
                Debug.LogError("[MessageFactory] Invalid buffer");
                return null;
            }
    
            try
            {
                var messageType = (MessageType)buffer[0];
        
                if (!messageCreators.TryGetValue(messageType, out var creator))
                {
                    Debug.LogWarning($"[MessageFactory] Unknown message type: {messageType}");
                    return null;
                }
        
                var message = creator();
                message.Deserialize(buffer);
        
                return message;
            }
            catch (Exception e)
            {
                Debug.LogError($"[MessageFactory] Failed to create message: {e.Message}");
                return null;
            }
        }
        
        public static void RegisterMessageType(MessageType type, Func<INetworkMessage> creator)
        {
            if (messageCreators.ContainsKey(type))
            {
                Debug.LogWarning($"[MessageFactory] Message type {type} already registered. Overwriting...");
            }
            messageCreators[type] = creator;
        }
    }
}
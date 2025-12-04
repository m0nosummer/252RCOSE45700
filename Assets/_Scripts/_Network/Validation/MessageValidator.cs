using System;
using UnityEngine;
using Arena.Core;
using Arena.Logging;

namespace Arena.Network.Validation
{
    public class MessageValidator
    {
        private readonly IGameLogger _logger;
        private float _lastTimestamp;
        private uint _lastSequenceNumber;

        public MessageValidator(IGameLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public ValidationResult Validate(INetworkMessage message)
        {
            if (message == null)
                return ValidationResult.NullMessage;

            if (message.Timestamp < _lastTimestamp - 1f)
            {
                _logger.Log(LogLevel.Warning, "Validation", 
                    "Timestamp went backwards: {0} < {1}", message.Timestamp, _lastTimestamp);
                return ValidationResult.TimestampError;
            }

            if (message is ISequencedMessage sequenced)
            {
                if (sequenced.SequenceNumber <= _lastSequenceNumber)
                {
                    _logger.Log(LogLevel.Warning, "Validation", 
                        "Duplicate or old sequence: {0} <= {1}", 
                        sequenced.SequenceNumber, _lastSequenceNumber);
                    return ValidationResult.DuplicatePacket;
                }
                _lastSequenceNumber = sequenced.SequenceNumber;
            }

            var result = ValidateByType(message);
            if (result != ValidationResult.Valid)
                return result;

            _lastTimestamp = message.Timestamp;
            return ValidationResult.Valid;
        }

        private ValidationResult ValidateByType(INetworkMessage message)
        {
            switch (message.Type)
            {
                case MessageType.PlayerInput:
                    return ValidatePlayerInput(message as Arena.Network.Messages.PlayerInputMessage);
                
                case MessageType.PlayerState:
                    return ValidatePlayerState(message as Arena.Network.Messages.PlayerStateMessage);
                
                default:
                    return ValidationResult.Valid;
            }
        }

        private ValidationResult ValidatePlayerInput(Arena.Network.Messages.PlayerInputMessage input)
        {
            if (input == null) return ValidationResult.InvalidData;

            
            if (input.MoveInput.magnitude > 1.5f) 
            {
                _logger.Log(LogLevel.Warning, "Validation", 
                    "Move input exceeds valid range: {0}", input.MoveInput.magnitude);
                return ValidationResult.InvalidData;
            }

            
            if (!IsPositionValid(input.MouseWorldPosition))
            {
                _logger.Log(LogLevel.Warning, "Validation", 
                    "Mouse position out of bounds: {0}", input.MouseWorldPosition);
                return ValidationResult.InvalidData;
            }

            return ValidationResult.Valid;
        }

        private ValidationResult ValidatePlayerState(Arena.Network.Messages.PlayerStateMessage state)
        {
            if (state == null) return ValidationResult.InvalidData;
            
            if (!IsPositionValid(state.Position))
            {
                _logger.Log(LogLevel.Warning, "Validation", 
                    "Position out of bounds: {0}", state.Position);
                return ValidationResult.InvalidData;
            }
            
            if (state.Health < 0 || state.Health > GameConstants.Gameplay.DefaultHealth * 1.1f)
            {
                _logger.Log(LogLevel.Warning, "Validation", 
                    "Invalid health value: {0}", state.Health);
                return ValidationResult.InvalidData;
            }
            
            if (state.Velocity.magnitude > GameConstants.Gameplay.DefaultMoveSpeed * 2f)
            {
                _logger.Log(LogLevel.Warning, "Validation", 
                    "Suspicious velocity: {0}", state.Velocity.magnitude);
                return ValidationResult.InvalidData;
            }

            return ValidationResult.Valid;
        }
        
        private bool IsPositionValid(Vector3 position)
        {
            return position.x >= GameConstants.Compression.MinPositionRange &&
                   position.x <= GameConstants.Compression.MaxPositionRange &&
                   position.y >= GameConstants.Compression.MinPositionRange &&
                   position.y <= GameConstants.Compression.MaxPositionRange &&
                   position.z >= GameConstants.Compression.MinPositionRange &&
                   position.z <= GameConstants.Compression.MaxPositionRange;
        }

        public void Reset()
        {
            _lastTimestamp = 0;
            _lastSequenceNumber = 0;
        }
    }

    public interface ISequencedMessage
    {
        uint SequenceNumber { get; }
    }

    public enum ValidationResult
    {
        Valid,
        NullMessage,
        TimestampError,
        DuplicatePacket,
        InvalidData,
        OutOfBounds,
        SuspiciousActivity
    }
}
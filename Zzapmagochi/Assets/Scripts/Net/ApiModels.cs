using System;
using System.Collections.Generic;

namespace Zzapmagochi.Net
{
    [Serializable]
    public class LoginRequest
    {
        public string studentId;
        public string password;
    }

    [Serializable]
    public class SignUpRequest
    {
        public string studentId;
        public string password;
    }

    [Serializable]
    public class AuthResponse
    {
        public string accessToken;
        public string refreshToken;
        public int expiresIn;
        public bool isNewUser;
        public string role;
    }

    [Serializable]
    public class IdAvailabilityResponse
    {
        public string studentId;
        public bool isAvailable;
    }

    [Serializable]
    public class CharacterPartsDto
    {
        public string outer;
        public string accessories;
        public string clothTop;
        public string clothBottom;
        public string shoe;
    }

    [Serializable]
    public class CharacterDataDto
    {
        public string nickname;
        public string persona;
        public string catchphrase;
        public string presetId;
        public CharacterPartsDto parts;
    }

    [Serializable]
    public class CharacterUpdateRequest
    {
        public string presetId;
        public CharacterPartsDto parts;
    }

    [Serializable]
    public class RoomPlacementDto
    {
        public string furnitureId;
        public int gridX;
        public int gridY;
        public int gridZ;
        public int rotation;
    }

    [Serializable]
    public class RoomLayoutDto
    {
        public string ownerStudentId;
        public string visibility;
        public string wallpaperId;
        public string floorId;
        public List<RoomPlacementDto> placements =
            new List<RoomPlacementDto>();
        public string updatedAt;
    }

    [Serializable]
    public class RoomUpdateRequest
    {
        public string visibility = "friends";
        public string wallpaperId;
        public string floorId;
        public List<RoomPlacementDto> placements =
            new List<RoomPlacementDto>();
    }

    [Serializable]
    public class RandomStudentIdResponse
    {
        public string studentId;
    }

    /// <summary>GET /room/{studentId} — 방문 대상의 캐릭터와 방을 한 번에 받는다.</summary>
    [Serializable]
    public class RoomVisitResponse
    {
        public string studentId;
        public CharacterDataDto character;
        public RoomLayoutDto room;
    }

    [Serializable]
    public class WalletDataDto
    {
        public int totalEarned;
        public int balance;
    }

    [Serializable]
    public class MeResponse
    {
        public string studentId;
        public string role;
        public CharacterDataDto character;
        public WalletDataDto wallet;
        public List<string> inventory = new List<string>();
        public RoomLayoutDto room;
    }

    public class ApiException : Exception
    {
        public long StatusCode { get; }
        public string Code { get; }

        public ApiException(
            long statusCode,
            string code,
            string message) : base(message)
        {
            StatusCode = statusCode;
            Code = code;
        }
    }

    public static class ApiErrorCode
    {
        public const string InvalidCredentials = "INVALID_CREDENTIALS";
        public const string InvalidRequest = "INVALID_REQUEST";
        public const string RateLimited = "RATE_LIMITED";
        public const string StudentIdTaken = "STUDENT_ID_TAKEN";
        public const string NetworkError = "NETWORK_ERROR";
    }
}

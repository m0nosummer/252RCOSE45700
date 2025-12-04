namespace Arena.Core
{
    public enum MessageType : byte
    {
        None = 0,
        Handshake = 1,
        PlayerInput = 2,
        PlayerState = 3,
        VisionData = 4,
        SkillUsed = 5,
        GameStart = 6,
        GameEnd = 7,
        PlayerDeath = 8,
        Heartbeat = 9,
        Fire = 10,              // Client→Server: 발사 요청
        BulletSpawn = 11,       // Server→Client: 총알 생성 명령
        BulletDestroy = 12,     // Server→Client: 총알 제거 명령
        Respawn = 13,
        PlayerJoined = 14,  
        PlayerLeft = 15
    }
}   
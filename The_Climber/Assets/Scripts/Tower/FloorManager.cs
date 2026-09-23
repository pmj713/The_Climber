using System;
using System.Collections.Generic;
using UnityEngine;

// Floor1 같은 탑 층 전용 씬 하나를 관리한다. 실제 몬스터 스폰은 씬에 배치된 각
// RoomEncounter가 알아서 하고, FloorManager는 이 층의 난이도 배율을 각 방에 적용하고
// 모든 방을 클리어했는지만 추적한다.
public class FloorManager : MonoBehaviour
{
    [SerializeField] private int floorNumber = 1;
    [SerializeField] private float healthMultiplier = 1f;
    [SerializeField] private float damageMultiplier = 1f;

    private readonly List<RoomEncounter> rooms = new List<RoomEncounter>();
    private int clearedRoomCount;

    // GameOverController가 HUD Canvas(마을/탑 씬 공통으로 유지되는 오브젝트)에서
    // 층 정보를 읽어야 하는데, FloorManager는 Floor1 같은 탑 전용 씬에만 있어서
    // Inspector로 직접 참조를 걸 수 없다. 그래서 다른 매니저들처럼 Instance로 노출한다.
    public static FloorManager Instance { get; private set; }

    public int CurrentFloorNumber => floorNumber;
    public int TotalRoomCount => rooms.Count;
    public int ClearedRoomCount => clearedRoomCount;

    public event Action<int> OnRoomCleared; // 남은 방 개수
    public event Action OnFloorCleared;

    private void Awake()
    {
        Instance = this;
    }

    // Floor1은 전용 씬이라 로드되자마자 바로 시작한다.
    private void Start()
    {
        BeginRun();
    }

    public void BeginRun()
    {
        rooms.Clear();
        clearedRoomCount = 0;
        rooms.AddRange(FindObjectsOfType<RoomEncounter>());

        if (rooms.Count == 0)
        {
            Debug.LogWarning("FloorManager: 씬에 RoomEncounter가 하나도 없습니다.");
            return;
        }

        foreach (RoomEncounter room in rooms)
        {
            room.ApplyFloorMultipliers(healthMultiplier, damageMultiplier);
            room.OnCleared += HandleRoomCleared;
        }

        Debug.Log($"{floorNumber}층 시작 - 방 {rooms.Count}개");
    }

    private void HandleRoomCleared(RoomEncounter room)
    {
        room.OnCleared -= HandleRoomCleared;
        clearedRoomCount++;

        int remaining = rooms.Count - clearedRoomCount;
        OnRoomCleared?.Invoke(remaining);

        if (remaining <= 0)
        {
            Debug.Log($"{floorNumber}층 클리어!");
            OnFloorCleared?.Invoke();
        }
    }
}

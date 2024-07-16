// #define DEBUG_FLAG

using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using Random = UnityEngine.Random;

namespace PuzzleSystem
{
    enum PuzzleSystemState
    {
        INITIALIZE,
        IDLE,
        IN_GAME,
    }

    enum GameState
    {
        IDLE,
        WAIT_START,
        IN_GAME,
        FINISH,
        RESULT,
    }

    enum GameDifficulty
    {
        EASY,
        NORMAL,
        DIFFICULT,
        __MAX__,
    }

    enum GameTargetArea
    {
        ALL,
        ONLY_MAIN,
        ONLY_REMOTE,
        __MAX__,
    }

    enum PieceUIMode
    {
        NONE,
        SIMPLE,
        DETAIL,
    }

    enum MessageType
    {
        REQUEST_SYNC_STATE,
        SYNC_STATE,
        START_GAME,
        CLEAR_GAME,
        RESET,
        PIECE_CORRECT,
        CHANGE_DIFFICULTY,
        CHANGE_TARGET_AREA,
    }

    enum IslandType
    {
        MAIN = 0,
        REMOTE = 1,
    }

    enum AudioClipID
    {
        TAB_CLICK = 0,
        PICKUP_PIECE = 1,
        COUNT_DOWN = 2,
        GAME_START = 3,
        GAME_CLEAR = 4,
        NEW_RECORD = 5,
        RESET = 6,
        PIECE_CORRECT = 7,
    }

    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PuzzleSystem : UdonSharpBehaviour
    {
        const string IDEOGRAPHIC_SPACE = "　";

        const float INITIALIZATION_TIME = 2;
        const int INFO_IDX_ID = 0;
        const int INFO_IDX_OBJ_NAME = 1;
        const int INFO_IDX_NAME = 2;
        const int INFO_IDX_NAME_KANA = 3;
        const int INFO_IDX_GROUP = 4;
        const int INFO_IDX_DESCRIPTION = 5;
        const int INFO_IDX_AREA = 6;
        const int INFO_IDX_AREA_UNIT = 7;
        const int INFO_IDX_POPULATION = 8;
        const int INFO_IDX_POPULATION_YEAR = 9;
        const int INFO_IDX_POPULATION_MONTH = 10;
        const int INFO_IDX_POPULATION_DAY = 11;
        const int INFO_IDX_POPULATION_DISPLAY_TYPE = 12;
        const int INFO_IDX_ISLAND_TYPE = 13;

        const float RIGHT_DISTANCE_EASY = 1;
        const float RIGHT_DISTANCE_NORMAL = 0.8f;
        const float RIGHT_DISTANCE_DIFFICULT = 0.4f;
        const float RIGHT_DISTANCE_ANGLE_NORMAL = 100;
        const float RIGHT_DISTANCE_ANGLE_DIFFICULT = 50;

        [SerializeField] Transform _puzzleObject;
        [SerializeField] string[] _pieceInfo;
        [SerializeField] string[] _movieInfo;
        [SerializeField] string[] _movieInfoTable;

        VRC_Pickup[] _piecePickups;
        bool[] _isPieceHeld;
        Transform[] _pieceInitPosAnchors;
        int _pieceInfoCount;
        PuzzleSystemState _state;
        GameState _gameState;
        bool[] _isOnRightPositions;
        int[] _pickupLastOwnerIds;

        Material[] _pieceMaterials;
        Color _defaultColor, _defaultColorEmission;
        [SerializeField] Color _notOnRightPosColor;
        [SerializeField] Color _notOnRightPosColorEmission;

        Transform[] _pickupInfoRoots;
        GameObject[] _simpleRoots, _detailRoots, _infoSimpleKana;
        IslandType[] _islandTypes;

        Toggle[] _difficultyToggles, _areaToggles;
        int _areaButtonClickedFrameCount, _difficultyButtonClickedFrameCount;
        Animator _gameSettingUIAnimator;

        float _initializeWaitTime;

        Vector3 _playerPos, _playerEyePos;

        void Start() { Initialize(); }
        void Update() { Update_(); }

        void Initialize()
        {
            _state = PuzzleSystemState.INITIALIZE;

            InitPieces();
            InitTimer();
            InitGameSettingUIs();
            InitPickupInfoUpdate();
            InitShufflePos();
            InitAudio();
            InitCountDownText();
            InitBestScoreText();
            InitDistanceDisplay();
            InitMarkers();
            InitPositionBoard();
            InitDetailInfoGlobal();

            {
                _defaultColor = _pieceMaterials[0].GetColor("_Color");
                _defaultColorEmission = _pieceMaterials[0].GetColor("_EmissionColor");
                SetPieceColorDefaultAll();
                SwitchPieceUIAll(PieceUIMode.SIMPLE);
                SetPiecePickupInteractionTextDefault();
            }

            {
                _currentDifficulty = GameDifficulty.EASY;
                _currentTargetArea = GameTargetArea.ONLY_MAIN;
                SetDifficultyUI(_currentDifficulty);
                SetTargetAreaUI(_currentTargetArea);
            }

            _initializeWaitTime = Time.time + INITIALIZATION_TIME;
        }

        void InitGameSettingUIs()
        {
            var root = transform.Find("#GamePanelUI");
            var canvasRoot = root.Find("#Canvas");
            _difficultyToggles = canvasRoot.Find("#Difficulity/#Tab/#Toggles").GetComponentsInChildren<Toggle>();
            _areaToggles = canvasRoot.Find("#Area/#Tab/#Toggles").GetComponentsInChildren<Toggle>();
            _gameSettingUIAnimator = root.GetComponent<Animator>();
        }

        void InitPieces()
        {
            _pieceInfoCount = _pieceInfo.Length;
            var pickupsRoot = transform.Find("#Pickups");

            _pieceInitPosAnchors = new Transform[_pieceInfoCount];
            var anchorBase = transform.Find("#AnchorBase").gameObject;
            anchorBase.SetActive(false);

            _piecePickups = new VRC_Pickup[_pieceInfoCount];
            _isPieceHeld = new bool[_pieceInfoCount];
            _pickupLastOwnerIds = new int[_pieceInfoCount];

            _startPositions = new Vector3[_pieceInfoCount];
            _nextPositions = new Vector3[_pieceInfoCount];
            _moveNextPositionCurrentSteps = new float[_pieceInfoCount];
            _isMovingPiecePositions = new bool[_pieceInfoCount];

            _startRotations = new Quaternion[_pieceInfoCount];
            _nextRotations = new Quaternion[_pieceInfoCount];
            _moveNextRotationCurrentSteps = new float[_pieceInfoCount];
            _isMovingPieceRotations = new bool[_pieceInfoCount];

            _isOnRightPositions = new bool[_pieceInfoCount];
            _pieceMaterials = new Material[_pieceInfoCount];

            _pickupInfoRoots = new Transform[_pieceInfoCount];
            _simpleRoots = new GameObject[_pieceInfoCount];
            _detailRoots = new GameObject[_pieceInfoCount];
            _infoSimpleKana = new GameObject[_pieceInfoCount];
            _islandTypes = new IslandType[_pieceInfoCount];

            for (int i = 0; i < pickupsRoot.childCount; i++)
            {
                pickupsRoot.GetChild(i).gameObject.SetActive(false);
            }

            for (int i = 0; i < _pieceInfoCount; i++)
            {
                var infos = _pieceInfo[i].Split(",");
                if (infos.Length == 0)
                {
                    continue;
                }

                var objName = infos[INFO_IDX_OBJ_NAME];
                var piece = _puzzleObject.Find(objName);
                if (piece == null)
                {
#if DEBUG_FLAG
                    Debug.LogWarning($"Not Found: {objName}");
#endif
                    continue;
                }

                if (i < _pieceInitPosAnchors.Length)
                {
                    var pieceInitPos = Instantiate(anchorBase).transform;
                    pieceInitPos.name = $"Anchor-{objName}";
                    pieceInitPos.parent = _puzzleObject;
                    pieceInitPos.SetPositionAndRotation(piece.position, piece.rotation);
                    pieceInitPos.localScale = piece.localScale;
                    _pieceInitPosAnchors[i] = pieceInitPos;
                }

                if (pickupsRoot.childCount <= i)
                {
                    continue;
                }

                var pickup = pickupsRoot.GetChild(i);
                pickup.gameObject.SetActive(true);

                _piecePickups[i] = pickup.GetComponent<VRC_Pickup>();

                var isPickupMoved = IsPickupMoved(pickup);
                if (!isPickupMoved)
                {
                    pickup.SetPositionAndRotation(piece.position, piece.rotation);
                }
                piece.parent = pickup;
                var scale = piece.localScale;
                piece.localScale = Vector3.one;
                pickup.localScale = scale;
                if (isPickupMoved)
                {
                    piece.localPosition = Vector3.zero;
                    piece.localEulerAngles = Vector3.zero;
                }

                var pieceMesh = piece.GetComponent<MeshFilter>().mesh;
                var pickupColMesh = pickup.GetComponent<MeshCollider>();
                pickupColMesh.sharedMesh = pieceMesh;

                _pieceMaterials[i] = piece.GetComponent<Renderer>().material;

                {
                    _pickupInfoRoots[i] = pickup.transform.Find("#PieceInfoUI");
                    var s = _pickupInfoRoots[i].localScale;
                    s.x /= scale.x;
                    s.y /= scale.y;
                    s.z /= scale.z;
                    _pickupInfoRoots[i].localScale = s;
                    InitPieceInfoUI(_pickupInfoRoots[i], infos, _movieInfo, _movieInfoTable[i], out var simpleRoot, out var detailRoot, out var simpleKana);
                    _simpleRoots[i] = simpleRoot;
                    _detailRoots[i] = detailRoot;
                    _infoSimpleKana[i] = simpleKana;
                    _islandTypes[i] = (IslandType)int.Parse(infos[INFO_IDX_ISLAND_TYPE]);
                }
            }
        }

        void SetPiecePickupInteractionTextDefault()
        {
            for (int i = 0; i < _piecePickups.Length; i++)
            {
                if (IsValidPieceIndex(i))
                {
                    _piecePickups[i].InteractionText = _pieceInfo[i].Split(",")[INFO_IDX_NAME];
                }
            }
        }

        const string PIECE_EMPTY_INTERACTION_TEXT = IDEOGRAPHIC_SPACE;
        void CheckAndSetPiecePickupInteractionTextDefault()
        {
            if (string.IsNullOrEmpty(_piecePickups[0].InteractionText) || _piecePickups[0].InteractionText == PIECE_EMPTY_INTERACTION_TEXT)
            {
                SetPiecePickupInteractionTextDefault();
            }
        }

        void ClearPiecePickupInteractionText()
        {
            for (int i = 0; i < _piecePickups.Length; i++)
            {
                if (IsValidPieceIndex(i))
                {
                    _piecePickups[i].InteractionText = PIECE_EMPTY_INTERACTION_TEXT;
                }
            }
        }

        void SetPieceColorWithTargetState()
        {
            for (int i = 0; i < _pieceMaterials.Length; i++)
            {
                if (IsCurrentGameTargetPiece(i))
                {
                    SetPieceColorNotOnRightPos(i);
                }
                else
                {
                    SetPieceColorDefault(i);
                }
            }
        }

        void SetPieceColorNotOnRightPos(int i)
        {
            SetPieceColor(i, _notOnRightPosColor, _notOnRightPosColorEmission, false);
        }

        void SetPieceColorDefaultAll()
        {
            for (int i = 0; i < _pieceMaterials.Length; i++)
            {
                SetPieceColorDefault(i);
            }
        }

        void SetPieceColorDefault(int i)
        {
            SetPieceColor(i, _defaultColor, _defaultColorEmission, true);
        }

        void SetPieceColor(int i, Color main, Color emission, bool random)
        {
            if (!IsValidPieceIndex(i))
            {
                return;
            }

            if (random)
            {
                var range = 0.15f;
                main.r += Random.Range(-range, range);
                main.g += Random.Range(-range, range);
                main.b += Random.Range(-range, range);
                emission.r += Random.Range(-range, range);
                emission.g += Random.Range(-range, range);
                emission.b += Random.Range(-range, range);
            }

            _pieceMaterials[i].SetColor("_Color", main);
            _pieceMaterials[i].SetColor("_EmissionColor", emission);
        }

        TextMeshProUGUI _timeText;
        float _startTime;
        float[][] _bestGameClearTimes;

        void InitTimer()
        {
            _timeText = transform.Find("#GamePanelUI/#Canvas/#TimeText").GetComponent<TextMeshProUGUI>();
            _bestGameClearTimes = new float[(int)GameTargetArea.__MAX__][];
            for (int i = 0; i < _bestGameClearTimes.Length; i++)
            {
                _bestGameClearTimes[i] = new float[(int)GameDifficulty.__MAX__];
                for (int j = 0; j < _bestGameClearTimes[i].Length; j++)
                {
                    _bestGameClearTimes[i][j] = float.MaxValue;
                }
            }
        }

        void UpdateTimer()
        {
            SetTimerText(GetTime());
        }

        void StartTimer()
        {
            _startTime = Time.time;
        }

        void SetTimerText(float time)
        {
            _timeText.text = GetTimeString(time);
        }

        float GetTime()
        {
            return Time.time - _startTime;
        }

        string GetTimeString(float time)
        {
            GetTimeElements(time, out var minutes, out var sec, out var millisec);
            return $"{minutes:D2}:{sec:D2}.{Mathf.RoundToInt(millisec * 1000):D3}";
        }

        void GetTimeElements(float time, out int minutes, out int sec, out float millisec)
        {
            int totalSeconds = Mathf.FloorToInt(time);
            millisec = time - totalSeconds;
            minutes = totalSeconds / 60;
            sec = totalSeconds % 60;
        }

        float GetBestGameClearTime(GameDifficulty difficulty, GameTargetArea area)
        {
            return _bestGameClearTimes[(int)area][(int)difficulty];
        }

        void SetBestGameClearTime(GameDifficulty difficulty, GameTargetArea area, float time)
        {
            _bestGameClearTimes[(int)area][(int)difficulty] = time;
        }

        bool IsPickupMoved(Transform pickup)
        {
            return !Mathf.Approximately(pickup.localPosition.x, 0)
                || !Mathf.Approximately(pickup.localPosition.y, 0)
                || !Mathf.Approximately(pickup.localPosition.z, 0)
                || !Mathf.Approximately(pickup.localEulerAngles.x, 0)
                || !Mathf.Approximately(pickup.localEulerAngles.y, 0)
                || !Mathf.Approximately(pickup.localEulerAngles.z, 0);
        }

        void Update_()
        {
            switch (_state)
            {
                case PuzzleSystemState.INITIALIZE:
                    {
                        if (Networking.LocalPlayer == null || _initializeWaitTime > Time.time)
                        {
                            return;
                        }
                        _state = PuzzleSystemState.IDLE;
                        SendNetwork_RequestSyncState();
                        return;
                    }
                case PuzzleSystemState.IDLE:
                    {
                        SetPlayerPos();
                        MovePieces();
                        CheckPiecePickups();
                        UpdatePickupInfo();

                        UpdateDistanceDisplay();
                        UpdateMarkers();
                        UpdatePositionBoard();
                        return;
                    }
                case PuzzleSystemState.IN_GAME:
                    {
                        SetPlayerPos();
                        MovePieces();
                        CheckPiecePickups();
                        UpdateGame();
                        UpdatePickupInfo();

                        UpdateDistanceDisplay();
                        UpdateMarkers();
                        UpdatePositionBoard();
                        return;
                    }
            }
        }

        void SetPlayerPos()
        {
            _playerPos = Networking.LocalPlayer.GetPosition();
            _playerEyePos = _playerPos + Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position.y * Vector3.up;
        }

        void CheckPiecePickups()
        {
            for (int i = 0; i < _pieceInfoCount; i++)
            {
                if (!IsValidPieceIndex(i))
                {
                    continue;
                }

                bool isHeld = _piecePickups[i].IsHeld;
                if (_isPieceHeld[i] != isHeld)
                {
                    if (_isPieceHeld[i] = isHeld)
                    {
                        OnPieceHeld(i, _piecePickups[i].currentPlayer);
                    }
                    else
                    {
                        OnPieceRelease(i);
                    }
                }
            }
        }

        void OnPieceHeld(int i, VRCPlayerApi player)
        {
            if (player != null)
            {
                _pickupLastOwnerIds[i] = player.playerId;
            }
            else
            {
                _pickupLastOwnerIds[i] = -1;
            }

            var isMyAction = _pickupLastOwnerIds[i] == Networking.LocalPlayer.playerId;

            if (IsInGame())
            {
                if (isMyAction)
                {
                    PlayAudio(_audioSource_Field, AudioClipID.PICKUP_PIECE);
                }

                if (_currentDifficulty == GameDifficulty.DIFFICULT)
                {
                    return;
                }

                SwitchPieceUI(i, PieceUIMode.SIMPLE, showSimpleKana: true);
            }
            else
            {
                if (isMyAction)
                {
                    SwitchPieceUI(i, PieceUIMode.DETAIL);
                }

                SetDetailInfoGlobal(i);
            }
        }

        void OnPieceRelease(int i)
        {
            var isMyAction = _pickupLastOwnerIds[i] == Networking.LocalPlayer.playerId;
            TurnToUpward(_piecePickups[i].transform);
            CheckAndMovePieceToValidPosition(i);

            if (!IsInGame() || _currentDifficulty != GameDifficulty.DIFFICULT)
            {
                SwitchPieceUI(i, PieceUIMode.SIMPLE);
            }

            if (IsInGame())
            {
                if (isMyAction)
                {
                    if (IsPieceOnRightPosition(i))
                    {
                        PlayAudio(_audioSource_Field, AudioClipID.PIECE_CORRECT);
                        OnPieceSetOnRightPosition(i, isMyAction);
                    }
                }
            }
        }

        void OnPieceSetOnRightPosition(int i, bool isMyAction)
        {
            SetEnablePickup(_piecePickups[i], false);
            _isOnRightPositions[i] = true;
            StartMovePieceToRightPos(i);
            SetPieceColorDefault(i);
            if (_currentDifficulty == GameDifficulty.DIFFICULT)
            {
                SwitchPieceUI(i, PieceUIMode.SIMPLE);
            }

            if (isMyAction)
            {
                if (IsClearGame())
                {
                    var clearTime = GetTime();
                    var beforeBestTime = GetBestGameClearTime(_currentDifficulty, _currentTargetArea);
                    bool isBest = clearTime < beforeBestTime && beforeBestTime != float.MaxValue;

                    SendNetwork_ClearGame(clearTime, isBest, i);
                    OnClearGame(clearTime, isBest);
                }
                else
                {
                    SendNetwork_PieceCorrect(i);
                }
            }
        }

        [SerializeField] float _piecePosMinY = 0.05f;
        void CheckAndMovePieceToValidPosition(int i)
        {
            if (_piecePickups[i].transform.position.y < _piecePosMinY)
            {
                var p = _piecePickups[i].transform.position;
                p.y = _piecePosMinY;
                StartMovePiece(i, p);
            }
        }

        void SetEnablePickupWithTargetState()
        {
            for (int i = 0; i < _piecePickups.Length; i++)
            {
                SetEnablePickup(_piecePickups[i], IsCurrentGameTargetPiece(i));
            }
        }

        void SetEnablePickupAll(bool pickupable)
        {
            for (int i = 0; i < _piecePickups.Length; i++)
            {
                SetEnablePickup(_piecePickups[i], pickupable);
            }
        }

        void SetEnablePickup(VRC_Pickup pickup, bool pickupable)
        {
            if (pickup != null)
            {
                pickup.pickupable = pickupable;
            }
        }

        bool IsInGame()
        {
            return _state == PuzzleSystemState.IN_GAME;
        }

        void TurnToUpward(Transform t)
        {
            t.RotateAround(t.position, Vector3.Cross(t.up, Vector3.up), Vector3.Angle(t.up, Vector3.up));
        }

        bool IsPieceOnRightPosition(int i)
        {
            switch (_currentDifficulty)
            {
                case GameDifficulty.EASY:
                    {
                        return IsSamePosition(_piecePickups[i].transform, _pieceInitPosAnchors[i], RIGHT_DISTANCE_EASY);
                    }
                case GameDifficulty.NORMAL:
                    {
                        return IsSamePosition(_piecePickups[i].transform, _pieceInitPosAnchors[i], RIGHT_DISTANCE_NORMAL)
                            && IsSameAngle(_piecePickups[i].transform, _pieceInitPosAnchors[i], RIGHT_DISTANCE_ANGLE_NORMAL);
                    }
                case GameDifficulty.DIFFICULT:
                default:
                    {
                        return IsSamePosition(_piecePickups[i].transform, _pieceInitPosAnchors[i], RIGHT_DISTANCE_DIFFICULT)
                            && IsSameAngle(_piecePickups[i].transform, _pieceInitPosAnchors[i], RIGHT_DISTANCE_ANGLE_DIFFICULT);
                    }
            }
        }

        bool IsSamePosition(Transform a, Transform b, float distance)
        {
            return (a.position - b.position).magnitude < distance;
        }

        bool IsSameAngle(Transform a, Transform b, float angle)
        {
            return Vector3.Angle(a.forward, b.forward) < angle;
        }

        Vector3[] _startPositions, _nextPositions;
        Quaternion[] _startRotations, _nextRotations;
        float[] _moveNextPositionCurrentSteps, _moveNextRotationCurrentSteps;
        bool[] _isMovingPiecePositions, _isMovingPieceRotations;

        void StartShufflePieces()
        {
            _pieceMoveSpeed = PIECE_MOVE_SPEED_SHUFFLE;

            var anchors = _currentTargetArea == GameTargetArea.ONLY_REMOTE ? _shufflePosAnchorsRemote : _shufflePosAnchorsMain;

            for (int i = 0; i < _pieceInfoCount; i++)
            {
                if (_piecePickups[i] == null)
                {
                    continue;
                }

                if (IsCurrentGameTargetPiece(i))
                {
                    StartMovePiece(i, GetRandomInitShufflePos(ref anchors));
                    StartRotatePiece(i, Quaternion.Euler(new Vector3(0, Random.Range(360, 360 * 3), 0)));
                }
                else
                {
                    StartMovePieceToRightPos(i);
                }
            }
        }

        void StartResetPiecePositions()
        {
            for (int i = 0; i < _pieceInfoCount; i++)
            {
                StartMovePieceToRightPos(i);
            }
        }

        void StartMovePieceToRightPos(int i)
        {
            if (!IsValidPieceIndex(i))
            {
                return;
            }

            StartMovePiece(i, _pieceInitPosAnchors[i].transform.position);
            StartRotatePiece(i, _pieceInitPosAnchors[i].transform.rotation);
        }

        void StartMovePiece(int i, Vector3 pos)
        {
            if (!IsValidPieceIndex(i))
            {
                return;
            }

            _startPositions[i] = _piecePickups[i].transform.position;
            _nextPositions[i] = pos;
            _moveNextPositionCurrentSteps[i] = 0;
            _isMovingPiecePositions[i] = true;
        }

        void StartRotatePiece(int i, Quaternion rot)
        {
            if (!IsValidPieceIndex(i))
            {
                return;
            }

            _startRotations[i] = _piecePickups[i].transform.rotation;
            _nextRotations[i] = rot;
            _moveNextRotationCurrentSteps[i] = 0;
            _isMovingPieceRotations[i] = true;
        }

        const float PIECE_MOVE_SPEED_DEFAULT = 4;
        const float PIECE_MOVE_SPEED_SHUFFLE = 1;
        float _pieceMoveSpeed = PIECE_MOVE_SPEED_DEFAULT;
        void MovePieces()
        {
            for (int i = 0; i < _pieceInfoCount; i++)
            {
                if (!IsValidPieceIndex(i))
                {
                    continue;
                }

                var addStep = Time.deltaTime * _pieceMoveSpeed;
                if (_isMovingPiecePositions[i])
                {
                    _moveNextPositionCurrentSteps[i] += addStep;
                    _piecePickups[i].transform.position = Vector3.Lerp(_startPositions[i], _nextPositions[i], _moveNextPositionCurrentSteps[i]);
                    if (_moveNextPositionCurrentSteps[i] > 1)
                    {
                        _isMovingPiecePositions[i] = false;
                    }
                }

                if (_isMovingPieceRotations[i])
                {
                    _moveNextRotationCurrentSteps[i] += addStep;
                    _piecePickups[i].transform.rotation = Quaternion.Lerp(_startRotations[i], _nextRotations[i], _moveNextRotationCurrentSteps[i]);
                    if (_moveNextRotationCurrentSteps[i] > 1)
                    {
                        _isMovingPieceRotations[i] = false;
                    }
                }
            }
        }

        public void OnButtonClicked_Start()
        {
            PlayAudio(_audioSource_Field, AudioClipID.GAME_START);

            var difficulty = GetGameDifficulty();
            var area = GetGameTargetArea();
            SendNetwork_StartGame(difficulty, area);
            StartGameStartCountdown(true, difficulty, area);
        }

        bool _isMyStart;
        GameDifficulty _currentDifficulty;
        GameTargetArea _currentTargetArea;

        void StartGameStartCountdown(bool isMyStart, GameDifficulty difficulty, GameTargetArea area)
        {
            SetDifficultyUI(difficulty);
            SetTargetAreaUI(area);
            _currentDifficulty = difficulty;
            _currentTargetArea = area;

            _isMyStart = isMyStart;
            _state = PuzzleSystemState.IN_GAME;
            _gameState = GameState.WAIT_START;
            _inGameWaitStartStep = 0;
            _nextActionTime = 0;
            SetAllIsOnRightPos(false);
            SetEnablePickupAll(false);
            switch (_currentDifficulty)
            {
                case GameDifficulty.DIFFICULT:
                    {
                        ClearPiecePickupInteractionText();
                        SwitchPieceUIAll(PieceUIMode.SIMPLE, showSimpleKana: false, hide: true);
                        break;
                    }
                default:
                    {
                        SwitchPieceUIAll(PieceUIMode.SIMPLE);
                        break;
                    }
            }
            SetDifficultyButtonInteractable(false);
            SetAreaButtonInteractable(false);
            CountDownAnimatorSetTrigger("CountDown");
        }

        void OnStartGame()
        {
            _gameState = GameState.IN_GAME;
            _pieceMoveSpeed = PIECE_MOVE_SPEED_DEFAULT;
            SetEnablePickupWithTargetState();
            StartTimer();
        }

        int _inGameWaitStartStep;
        float _nextActionTime;
        const int STEP_TO_START = 4;

        void UpdateGame()
        {
            switch (_gameState)
            {
                case GameState.WAIT_START:
                    {
                        if (_nextActionTime <= Time.time)
                        {
#if DEBUG_FLAG
                            Debug.Log(_inGameWaitStartStep);
#endif
                            if (_inGameWaitStartStep == 0)
                            {
                                _nextActionTime = Time.time + 1;
                                _inGameWaitStartStep++;
                            }
                            else if (_inGameWaitStartStep <= 4)
                            {
                                if (_inGameWaitStartStep == 2)
                                {
                                    if (_isMyStart)
                                    {
                                        SetAllPickupOwnerToMe();
                                    }
                                }
                                else if (_inGameWaitStartStep == 3)
                                {
                                    if (_isMyStart)
                                    {
                                        StartShufflePieces();
                                    }
                                    SetPieceColorWithTargetState();
                                }
                                _nextActionTime = Time.time + 1;
                                _inGameWaitStartStep++;
                                PlayAudio(_audioSource_Field, AudioClipID.COUNT_DOWN);
                            }
                            else
                            {
                                PlayAudio(_audioSource_Field, AudioClipID.GAME_START);
                                OnStartGame();
                            }
                        }
                        return;
                    }
                case GameState.IN_GAME:
                    {
                        UpdateTimer();
                        return;
                    }
                case GameState.FINISH:
                    {
                        if (_nextActionTime <= Time.time)
                        {
                            _gameState = GameState.RESULT;
                        }

                        return;
                    }
                case GameState.RESULT:
                    {
                        if (_isBest)
                        {
                            PlayAudio(_audioSource_Field, AudioClipID.NEW_RECORD);
                            CountDownAnimatorSetTrigger("NewRecord");
                        }
                        ResetAll();
                        return;
                    }
            }
        }

        bool IsClearGame()
        {
            for (int i = 0; i < _isOnRightPositions.Length; i++)
            {
                if (!_isOnRightPositions[i] && IsValidPieceIndex(i) && IsCurrentGameTargetPiece(i))
                {
                    return false;
                }
            }
            return true;
        }

        bool _isBest;
        const int WAIT_FINISH_TIME = 2;
        void OnClearGame(float clearTime, bool isBest)
        {
            PlayAudio(_audioSource_Field, AudioClipID.GAME_CLEAR);

            _gameState = GameState.FINISH;

            _isBest = isBest;
            if (clearTime < GetBestGameClearTime(_currentDifficulty, _currentTargetArea))
            {
                SetBestGameClearTime(_currentDifficulty, _currentTargetArea, clearTime);
                UpdateBestScoreUI(_currentDifficulty, _currentTargetArea, clearTime);
            }

            SetTimerText(clearTime);

            CountDownAnimatorSetTrigger("GameClear");
            _gameSettingUIAnimator.SetTrigger("Blink");

            _nextActionTime = Time.time + WAIT_FINISH_TIME;
        }

        public void OnButtonClicked_Reset()
        {
            PlayAudio(_audioSource_Field, AudioClipID.RESET);

            SendNetwork_Reset();
            ResetAll();
        }

        void ResetAll()
        {
            _state = PuzzleSystemState.IDLE;
            _gameState = GameState.IDLE;
            _pieceMoveSpeed = PIECE_MOVE_SPEED_DEFAULT;
            CountDownAnimatorSetTrigger("Reset");
            SwitchPieceUIAll(PieceUIMode.SIMPLE);
            StartResetPiecePositions();
            SetPieceColorDefaultAll();
            SetEnablePickupAll(true);
            SetDifficultyButtonInteractable(true);
            SetAreaButtonInteractable(true);
            CheckAndSetPiecePickupInteractionTextDefault();
        }

        void CountDownAnimatorSetTrigger(string trigger)
        {
            if (trigger != "Reset")
            {
                _countDownAnimator.ResetTrigger("Reset");
            }
            _countDownAnimator.SetTrigger(trigger);
        }

        void SetAllIsOnRightPos(bool active)
        {
            for (int i = 0; i < _isOnRightPositions.Length; i++)
            {
                _isOnRightPositions[i] = active;
            }
        }

        bool IsValidPieceIndex(int i)
        {
            return _piecePickups[i] != null;
        }

        public void OnDifficultyButtonClicked()
        {
            PlayAudio(_audioSource_GamePanelUI, AudioClipID.TAB_CLICK);

            for (int i = 0; i < _difficultyToggles.Length; i++)
            {
                if (_difficultyToggles[i].isOn)
                {
                    if (_difficultyButtonClickedFrameCount != Time.frameCount)
                    {
                        _difficultyButtonClickedFrameCount = Time.frameCount;
                        OnDifficultyButtonClicked((GameDifficulty)i);
                    }
                }
            }
        }

        void OnDifficultyButtonClicked(GameDifficulty difficulty)
        {
            SendNetwork_ChangeDifficulty(difficulty);
        }

        void SetDifficultyUI(GameDifficulty difficulty)
        {
            if (IsInGame())
            {
                return;
            }

            for (int i = 0; i < _difficultyToggles.Length; i++)
            {
                _difficultyToggles[i].SetIsOnWithoutNotify(i == (int)difficulty);
            }
        }

        GameDifficulty GetGameDifficulty()
        {
            for (int i = 0; i < _difficultyToggles.Length; i++)
            {
                if (_difficultyToggles[i].isOn)
                {
                    return (GameDifficulty)i;
                }
            }
            return GameDifficulty.EASY;
        }

        void SetDifficultyButtonInteractable(bool interactable)
        {
            for (int i = 0; i < _difficultyToggles.Length; i++)
            {
                _difficultyToggles[i].interactable = interactable;
            }
        }

        public void OnAreaButtonClicked()
        {
            PlayAudio(_audioSource_GamePanelUI, AudioClipID.TAB_CLICK);

            for (int i = 0; i < _areaToggles.Length; i++)
            {
                if (_areaToggles[i].isOn)
                {
                    if (_areaButtonClickedFrameCount != Time.frameCount)
                    {
                        _areaButtonClickedFrameCount = Time.frameCount;
                        OnAreaButtonClicked((GameTargetArea)i);
                    }
                }
            }
        }

        void OnAreaButtonClicked(GameTargetArea area)
        {
            SendNetwork_ChangeTargetArea(area);
        }

        void SetTargetAreaUI(GameTargetArea area)
        {
            if (IsInGame())
            {
                return;
            }

            for (int i = 0; i < _areaToggles.Length; i++)
            {
                _areaToggles[i].SetIsOnWithoutNotify(i == (int)area);
            }
        }

        GameTargetArea GetGameTargetArea()
        {
            for (int i = 0; i < _areaToggles.Length; i++)
            {
                if (_areaToggles[i].isOn)
                {
                    return (GameTargetArea)i;
                }
            }
            return GameTargetArea.ONLY_MAIN;
        }

        void SetAreaButtonInteractable(bool interactable)
        {
            for (int i = 0; i < _areaToggles.Length; i++)
            {
                _areaToggles[i].interactable = interactable;
            }
        }

        void InitPieceInfoUI(Transform root, string[] info, string[] movieTitles, string movieIdsString, out GameObject simpleRoot, out GameObject detailRoot, out GameObject simpleKana)
        {
            var canvasRoot = root.Find("#Canvas");
            canvasRoot.GetComponent<BoxCollider>().size = Vector3.zero;

            simpleRoot = canvasRoot.Find("#Simple").gameObject;
            detailRoot = canvasRoot.Find("#Detail").gameObject;

            simpleKana = simpleRoot.transform.Find("#TextKana").gameObject;
            simpleKana.GetComponent<TextMeshProUGUI>().text = info[INFO_IDX_NAME_KANA];
            simpleRoot.transform.Find("#Text").GetComponent<TextMeshProUGUI>().text = info[INFO_IDX_NAME];

            detailRoot.transform.Find("#TextTitleKana").GetComponent<TextMeshProUGUI>().text = info[INFO_IDX_NAME_KANA];
            detailRoot.transform.Find("#TextTitle").GetComponent<TextMeshProUGUI>().text = info[INFO_IDX_NAME];
            var group = !string.IsNullOrEmpty(info[INFO_IDX_GROUP]) && info[INFO_IDX_GROUP] != "-" ? $"{info[INFO_IDX_GROUP]}\n" : string.Empty;
            var area = $"面積: {info[INFO_IDX_AREA]}{info[INFO_IDX_AREA_UNIT]}";
            var population = GetPopulationText(info);
            detailRoot.transform.Find("#TextHeaderInfo").GetComponent<TextMeshProUGUI>().text = $"{group}{area}\n{population}";
            detailRoot.transform.Find("#TextDescription").GetComponent<TextMeshProUGUI>().text = info[INFO_IDX_DESCRIPTION].Replace("\\n", "\n");

            var movieInfoRoot = detailRoot.transform.Find("#MovieInfo");
            var movieIds = movieIdsString.Split(",");
            var hasMovieInfo = !string.IsNullOrEmpty(movieIdsString) && movieIds.Length > 0;
            movieInfoRoot.gameObject.SetActive(hasMovieInfo);
            if (hasMovieInfo)
            {
                GameObject baseItem = null;
                var movieInfoItemRoot = movieInfoRoot.transform.Find("#Movies");
                for (int i = 0; i < movieIds.Length; i++)
                {
                    Transform elm = null;
                    if (i == 0)
                    {
                        elm = movieInfoItemRoot.GetChild(0);
                        baseItem = elm.gameObject;
                    }
                    else
                    {
                        elm = Instantiate(baseItem).transform;
                        elm.SetParent(movieInfoItemRoot, false);
                        elm.localPosition = new Vector3(0, -i * 10, 0);
                    }
                    elm.Find("#Title").GetComponent<TextMeshProUGUI>().text = movieTitles[int.Parse(movieIds[i])];
                }
            }
        }

        string GetPopulationText(string[] info)
        {
            if (int.TryParse(info[INFO_IDX_POPULATION_DISPLAY_TYPE], out var displayType) && displayType == 1)
            {
                if (info[INFO_IDX_POPULATION] == "-")
                {
                    return $"人口: -";
                }
                return $"人口: {info[INFO_IDX_POPULATION]} ({info[INFO_IDX_POPULATION_YEAR]}年)";
            }
            return int.TryParse(info[INFO_IDX_POPULATION], out var value) ? $"人口: {value:N0} ({info[INFO_IDX_POPULATION_YEAR]}年)" : $"人口: -";
        }

        void SwitchPieceUIAll(PieceUIMode mode, bool showSimpleKana = false, bool hide = false)
        {
            for (int i = 0; i < _simpleRoots.Length; i++)
            {
                SwitchPieceUI(i, mode, showSimpleKana: showSimpleKana, hide: hide);
            }
        }

        void SwitchPieceUI(int i, PieceUIMode mode, bool showSimpleKana = false, bool hide = false)
        {
            if (!IsValidPieceIndex(i))
            {
                return;
            }

            if (hide)
            {
                if (_simpleRoots[i].activeSelf)
                {
                    _simpleRoots[i].SetActive(false);
                }
                if (_detailRoots[i].activeSelf)
                {
                    _detailRoots[i].SetActive(false);
                }
                return;
            }

            _simpleRoots[i].SetActive(mode == PieceUIMode.SIMPLE);
            _detailRoots[i].SetActive(mode == PieceUIMode.DETAIL);

            if (mode == PieceUIMode.SIMPLE)
            {
                if (_infoSimpleKana[i].activeSelf != showSimpleKana)
                {
                    _infoSimpleKana[i].SetActive(showSimpleKana);
                }
            }
        }

        int[][] _pickupInfoUpdateRange;
        const int PICKUP_INFO_UPDATE_FRAME = 10;
        void InitPickupInfoUpdate()
        {

            _pickupInfoUpdateRange = new int[PICKUP_INFO_UPDATE_FRAME][];
            var step = _pickupInfoRoots.Length / PICKUP_INFO_UPDATE_FRAME + 1;
            for (int j = 0; j < 10; j++)
            {
                _pickupInfoUpdateRange[j] = new int[2];
                _pickupInfoUpdateRange[j][0] = step * j;
                _pickupInfoUpdateRange[j][1] = Mathf.Min(step * (j + 1), _pickupInfoRoots.Length);
            }
        }

        void UpdatePickupInfo()
        {
            var frameMod = Time.frameCount % 10;
            for (int i = _pickupInfoUpdateRange[frameMod][0]; i < _pickupInfoUpdateRange[frameMod][1]; i++)
            {
                if (_pickupInfoRoots[i] != null)
                {
                    var dir = _playerEyePos - _pickupInfoRoots[i].position;
                    _pickupInfoRoots[i].forward = -dir;
                }
            }
        }

        Transform[] _shufflePosAnchorsMain, _shufflePosAnchorsRemote;
        void InitShufflePos()
        {
            {
                var posRoot = transform.Find("#PieceShufflePos/#Main");
                _shufflePosAnchorsMain = new Transform[posRoot.childCount];
                for (int i = 0; i < posRoot.childCount; i++)
                {
                    _shufflePosAnchorsMain[i] = posRoot.GetChild(i);
                }
            }

            {
                var posRoot = transform.Find("#PieceShufflePos/#Remote");
                _shufflePosAnchorsRemote = new Transform[posRoot.childCount];
                for (int i = 0; i < posRoot.childCount; i++)
                {
                    _shufflePosAnchorsRemote[i] = posRoot.GetChild(i);
                }
            }
        }

        Vector3 GetRandomInitShufflePos(ref Transform[] anchors)
        {
            var i = Random.Range(0, anchors.Length);
            return Vector3.Lerp(anchors[i].position, anchors[(i + 1) % anchors.Length].position, Random.Range(0, 1f)) + Vector3.up * _piecePosMinY;
        }

        bool IsCurrentGameTargetPiece(int i)
        {
            switch (_currentTargetArea)
            {
                case GameTargetArea.ONLY_MAIN:
                    {
                        return _islandTypes[i] == IslandType.MAIN;
                    }
                case GameTargetArea.ONLY_REMOTE:
                    {
                        return _islandTypes[i] == IslandType.REMOTE;
                    }
                default:
                    {
                        return true;
                    }
            }
        }

        AudioSource _audioSource_GamePanelUI, _audioSource_Field;
        void InitAudio()
        {
            _audioSource_GamePanelUI = transform.Find("#GamePanelUI/#Audio").GetComponent<AudioSource>();
            _audioSource_GamePanelUI.enabled = true;

            _audioSource_Field = transform.Find("#CountText/#Audio").GetComponent<AudioSource>();
            _audioSource_Field.enabled = true;
        }

        void SetAllPickupOwnerToMe()
        {
            if (Networking.LocalPlayer == null)
            {
                return;
            }

            foreach (var pickup in _piecePickups)
            {
                if (!Networking.IsOwner(Networking.LocalPlayer, pickup.gameObject))
                {
                    Networking.SetOwner(Networking.LocalPlayer, pickup.gameObject);
                }
            }
        }

        Animator _countDownAnimator;
        void InitCountDownText()
        {
            _countDownAnimator = transform.Find("#CountText/#CountText").GetComponent<Animator>();
        }

        TextMeshProUGUI[][] _bestScoreTexts;
        [SerializeField] Color _bestScoreTextColor = new Color(0.18f, 0.86f, 0.13f);
        void InitBestScoreText()
        {
            var bestScoreRoot = transform.Find("#ResultUI/#Canvas/#BestScores");
            _bestScoreTexts = new TextMeshProUGUI[(int)GameTargetArea.__MAX__][];
            for (int j = 0; j < (int)GameTargetArea.__MAX__; j++)
            {
                _bestScoreTexts[j] = new TextMeshProUGUI[(int)GameDifficulty.__MAX__];
                for (int i = 0; i < (int)GameDifficulty.__MAX__; i++)
                {
                    var obj = bestScoreRoot.Find($"#Time_{j},{i}");
                    if (obj != null)
                    {
                        _bestScoreTexts[j][i] = obj.GetComponent<TextMeshProUGUI>();
                    }
                }
            }
        }

        void UpdateBestScoreUI(GameDifficulty difficulty, GameTargetArea area, float time)
        {
            int i = (int)difficulty;
            int j = (int)area;
            if (j < _bestScoreTexts.Length && i < _bestScoreTexts[j].Length && _bestScoreTexts[j][i] != null)
            {
                _bestScoreTexts[j][i].text = GetTimeString(time);
                _bestScoreTexts[j][i].color = _bestScoreTextColor;
            }
        }

        const float DISTANCE_GAME_TO_REAL = 50000 / 4.3133f;
        const int DISTANCE_MARKER_COUNT = 2;
        [SerializeField] VRC_Pickup[] _distanceMarkers;
        [SerializeField] Transform _distanceLine;
        Transform[] _distanceMarkerRoots = new Transform[DISTANCE_MARKER_COUNT];
        TextMeshProUGUI[] _distanceMarkerDistanceTexts = new TextMeshProUGUI[DISTANCE_MARKER_COUNT];
        GameObject[] _distanceMarkerWarnintTexts = new GameObject[DISTANCE_MARKER_COUNT];

        bool[] _distanceMarkerIsHeld = new bool[DISTANCE_MARKER_COUNT];
        int _distanceMarkerUpdatedFrameCount;
        Transform _boundL1, _boundL2_0, _boundL2_1, _boundL3_0, _boundL3_1;
        Transform[][] _regionAnchors_Puzzle, _regionAnchors_SemiReal, _regionAnchors_Board;

        void InitDistanceDisplay()
        {
            for (int i = 0; i < DISTANCE_MARKER_COUNT; i++)
            {
                _distanceMarkerRoots[i] = _distanceMarkers[i].transform.Find("#DistanceMarker");
                var canvasRoot = _distanceMarkerRoots[i].Find("#Canvas");
                canvasRoot.GetComponent<BoxCollider>().size = Vector3.zero;
                _distanceMarkerDistanceTexts[i] = canvasRoot.Find("#Distance").GetComponent<TextMeshProUGUI>();
                _distanceMarkerWarnintTexts[i] = canvasRoot.Find("#WarningText").gameObject;
                _distanceMarkerWarnintTexts[i].SetActive(false);
            }

            {
                var boundsRoot = transform.Find("#MeasuresPuzzle/#Bounds");
                _boundL1 = boundsRoot.Find("#L1");
                _boundL2_0 = boundsRoot.Find("#L2_0");
                _boundL2_1 = boundsRoot.Find("#L2_1");
                _boundL3_0 = boundsRoot.Find("#L3_0");
                _boundL3_1 = boundsRoot.Find("#L3_1");
            }

            InitMapAnchors(transform.Find("#MeasuresPuzzle/#Anchors"), ref _regionAnchors_Puzzle);
            InitMapAnchors(transform.Find("#MeasuresReal/#Anchors"), ref _regionAnchors_SemiReal);
            InitMapAnchors(transform.Find("#MapBoard/#Anchors"), ref _regionAnchors_Board);
        }

        void InitMapAnchors(Transform root, ref Transform[][] anchors)
        {
            anchors = new Transform[root.childCount][];
            for (int j = 0; j < root.childCount; j++)
            {
                anchors[j] = new Transform[2];
                for (int i = 0; i < anchors[j].Length; i++)
                {
                    anchors[j][i] = root.Find($"#R{j}/#p{i}");
                }
            }
        }

        void UpdateDistanceDisplay()
        {
            CheckDistanceDisplayPickup();

            if (Time.frameCount % 10 != 0)
            {
                return;
            }

            if (IsDistanceMarkerHeld())
            {
                UpdateDistanceMarkers();
            }
            else
            {
                UpdateDistanceMarkerDirections();
            }
        }

        bool IsDistanceMarkerHeld()
        {
            for (int i = 0; i < DISTANCE_MARKER_COUNT; i++)
            {
                if (_distanceMarkers[i].IsHeld)
                {
                    return true;
                }
            }
            return false;
        }

        void CheckDistanceDisplayPickup()
        {
            for (int i = 0; i < DISTANCE_MARKER_COUNT; i++)
            {
                if (_distanceMarkerIsHeld[i] != _distanceMarkers[i].IsHeld)
                {
                    _distanceMarkerIsHeld[i] = _distanceMarkers[i].IsHeld;
                    if (!_distanceMarkerIsHeld[i])
                    {
                        OnDistanceMarkerRelease();
                    }
                }
            }
        }

        void OnDistanceMarkerRelease()
        {
            for (int i = 0; i < DISTANCE_MARKER_COUNT; i++)
            {
                TurnToUpward(_distanceMarkers[i].transform);

                if (_distanceMarkers[i].transform.position.y < _piecePosMinY)
                {
                    var p = _distanceMarkers[i].transform.position;
                    p.y = _piecePosMinY;
                    _distanceMarkers[i].transform.position = p;
                }
            }

            UpdateDistanceMarkers();
        }

        void UpdateDistanceMarkers()
        {
            if (_distanceMarkerUpdatedFrameCount == Time.frameCount)
            {
                return;
            }
            _distanceMarkerUpdatedFrameCount = Time.frameCount;

            var realDistance = GetMarkerPuzzleDistance(_distanceMarkers[0].transform, _distanceMarkers[1].transform, out int regionA, out int regionB) * DISTANCE_GAME_TO_REAL;
            SetDistanceMarkerText(realDistance);

            var showDifferentRegionWarning = regionA != regionB;
            for (int i = 0; i < DISTANCE_MARKER_COUNT; i++)
            {
                if (_distanceMarkerWarnintTexts[i].activeSelf != showDifferentRegionWarning)
                {
                    _distanceMarkerWarnintTexts[i].SetActive(showDifferentRegionWarning);
                }
            }

            _distanceLine.position = (_distanceMarkers[0].transform.position + _distanceMarkers[1].transform.position) * 0.5f;
            _distanceLine.forward = _distanceMarkers[0].transform.position - _distanceMarkers[1].transform.position;
            var s = _distanceLine.localScale;
            s.z = Vector3.Distance(_distanceMarkers[0].transform.position, _distanceMarkers[1].transform.position);
            _distanceLine.localScale = s;

            UpdateDistanceMarkerDirections();
        }

        void UpdateDistanceMarkerDirections()
        {
            for (int i = 0; i < DISTANCE_MARKER_COUNT; i++)
            {
                var dir = _playerPos - _distanceMarkerRoots[i].position;
                dir.y = 0;
                _distanceMarkerRoots[i].forward = -dir;
            }
        }

        float GetMarkerPuzzleDistance(Transform a, Transform b, out int regionA, out int regionB)
        {
            regionA = GetRegion(a);
            regionB = GetRegion(b);

            var convertedPosA = PuzzleToSemiRealPos(regionA, a);
            var convertedPosB = PuzzleToSemiRealPos(regionB, b);

            var dir = convertedPosA - convertedPosB;
            dir.y = 0;
            return dir.magnitude;
        }

        int GetRegion(Transform a)
        {
            return GetRegion(a.transform.position);
        }

        int GetRegion(Vector3 p)
        {
            if (_boundL2_1.position.z < p.z && p.z < _boundL2_0.position.z && _boundL2_1.position.x < p.x && p.x < _boundL2_0.position.x)
            {
                return 2;
            }
            if (_boundL3_1.position.z < p.z && p.z < _boundL3_0.position.z && _boundL3_1.position.x < p.x && p.x < _boundL3_0.position.x)
            {
                return 3;
            }
            if (p.z < _boundL1.position.z)
            {
                return 1;
            }

            return 0;
        }

        Vector3 PuzzleToSemiRealPos(int region, Transform t)
        {
            return t.position - _regionAnchors_Puzzle[region][0].position + _regionAnchors_SemiReal[region][0].position;
        }

        void SetDistanceMarkerText(float distance)
        {
            var text = DistanceToDisplayText(distance);
            _distanceMarkerDistanceTexts[0].text = text;
            _distanceMarkerDistanceTexts[1].text = distance > 10000 ? text : string.Empty;
        }

        string DistanceToDisplayText(float distance)
        {
            if (distance < 1000)
            {
                return $"{(Mathf.Round(distance * 10) * 0.1f).ToString("F1")}m";
            }

            if (distance > 1000000)
            {
                return $"{Mathf.Round(distance * 0.001f):N0}km";
            }

            return $"{(Mathf.Round(distance * 0.001f * 10) * 0.1f).ToString("F1")}km";
        }

        [SerializeField] VRC_Pickup[] _markers;
        bool[] _markerIsHeld;

        void InitMarkers()
        {
            _markerIsHeld = new bool[_markers.Length];
        }

        void UpdateMarkers()
        {
            CheckMarkerPickup();
        }

        void CheckMarkerPickup()
        {
            for (int i = 0; i < _markers.Length; i++)
            {
                if (_markerIsHeld[i] != _markers[i].IsHeld)
                {
                    _markerIsHeld[i] = _markers[i].IsHeld;
                    if (!_markerIsHeld[i])
                    {
                        OnMarkerRelease(i);
                    }
                }
            }
        }

        void OnMarkerRelease(int i)
        {
            if (_markers[i].transform.position.y < _piecePosMinY)
            {
                var p = _markers[i].transform.position;
                p.y = _piecePosMinY;
                _markers[i].transform.position = p;
            }
        }

        [SerializeField] Transform _positionBoardMarker_player;
        [SerializeField] Transform[] _positionBoardMarkers_puzzle;
        [SerializeField] Transform[] _positionBoardMarkers_board;
        Transform[] _rangeAnchors_Board;
        float _puzzleToBoard;

        void InitPositionBoard()
        {
            _puzzleToBoard = (_regionAnchors_Board[0][1].position - _regionAnchors_Board[0][0].position).magnitude / (_regionAnchors_Puzzle[0][1].position - _regionAnchors_Puzzle[0][0].position).magnitude;

            _rangeAnchors_Board = new Transform[2];
            _rangeAnchors_Board[0] = transform.Find("#MapBoard/#RangeAnchors/#p0");
            _rangeAnchors_Board[1] = transform.Find("#MapBoard/#RangeAnchors/#p1");
        }

        void UpdatePositionBoard()
        {
            var fc = Time.frameCount % 25;
            if (_positionBoardMarkers_puzzle.Length < fc)
            {
                return;
            }

            var region = fc == _positionBoardMarkers_puzzle.Length ? GetRegion(_playerPos) : GetRegion(_positionBoardMarkers_puzzle[fc]);
            var pos = fc == _positionBoardMarkers_puzzle.Length ? _playerPos : _positionBoardMarkers_puzzle[fc].position;
            var dir = pos - _regionAnchors_Puzzle[region][0].position;

            var p = _puzzleToBoard * GameToBoardBase(dir) + _regionAnchors_Board[region][0].position;
            p.x = Mathf.Clamp(p.x, _rangeAnchors_Board[1].position.x, _rangeAnchors_Board[0].position.x);
            p.y = Mathf.Clamp(p.y, _rangeAnchors_Board[1].position.y, _rangeAnchors_Board[0].position.y);
            p.z = Mathf.Clamp(p.z, _rangeAnchors_Board[0].position.z, _rangeAnchors_Board[1].position.z);

            (fc == _positionBoardMarkers_puzzle.Length ? _positionBoardMarker_player : _positionBoardMarkers_board[fc]).position = p;
        }

        Vector3 GameToBoardBase(Vector3 pos)
        {
            return new Vector3(-pos.y, pos.z, -pos.x);
        }

        [SerializeField] AudioClip[] _audioClips;
        [SerializeField] float[] _audioVolumes;
        void PlayAudio(AudioSource source, AudioClipID clipID)
        {
            if (!source.enabled)
            {
                source.enabled = true;
            }
            var idx = (int)clipID;
            source.clip = _audioClips[idx];
            source.volume = _audioVolumes[idx];
            source.Play();
        }

        const int DETAIL_INFO_GLOBAL_COUNT = 2;
        Transform _detailInfoGlobal;
        TextMeshProUGUI[] _detailInfoGlobalTextTitle, _detailInfoGlobalTextTitleKana, _detailInfoGlobalHeaderInfoText, _detailInfoGlobalHeaderDescriptionText;
        Transform[] _detailInfoGlobalMovieRoot;
        int[] _detailInfoGlobalCurrentDisplayedIds;
        int _currentDetailInfoGlobalIdx;

        void InitDetailInfoGlobal()
        {
            _detailInfoGlobalTextTitle = new TextMeshProUGUI[DETAIL_INFO_GLOBAL_COUNT];
            _detailInfoGlobalTextTitleKana = new TextMeshProUGUI[DETAIL_INFO_GLOBAL_COUNT];
            _detailInfoGlobalHeaderInfoText = new TextMeshProUGUI[DETAIL_INFO_GLOBAL_COUNT];
            _detailInfoGlobalHeaderDescriptionText = new TextMeshProUGUI[DETAIL_INFO_GLOBAL_COUNT];
            _detailInfoGlobalCurrentDisplayedIds = new int[DETAIL_INFO_GLOBAL_COUNT];
            _detailInfoGlobalMovieRoot = new Transform[DETAIL_INFO_GLOBAL_COUNT];
            _detailInfoGlobal = transform.Find("#DetailInfoGlobal");
            for (int i = 0; i < DETAIL_INFO_GLOBAL_COUNT; i++)
            {
                var detailRoot = _detailInfoGlobal.GetChild(i).Find("#Canvas/#Detail");
                _detailInfoGlobalTextTitleKana[i] = detailRoot.transform.Find("#TextTitleKana").GetComponent<TextMeshProUGUI>();
                _detailInfoGlobalTextTitle[i] = detailRoot.transform.Find("#TextTitle").GetComponent<TextMeshProUGUI>();
                _detailInfoGlobalHeaderInfoText[i] = detailRoot.transform.Find("#TextHeaderInfo").GetComponent<TextMeshProUGUI>();
                _detailInfoGlobalHeaderDescriptionText[i] = detailRoot.transform.Find("#TextDescription").GetComponent<TextMeshProUGUI>();
                _detailInfoGlobalMovieRoot[i] = detailRoot.transform.Find("#MovieInfo");
            }

            for (int i = 0; i < DETAIL_INFO_GLOBAL_COUNT; i++)
            {
                _detailInfoGlobalCurrentDisplayedIds[i] = -1;
            }

            SetDetailInfoGlobal(0);
            SetDetailInfoGlobal(1);
        }

        void SetDetailInfoGlobal(int i)
        {
            if (Array.IndexOf(_detailInfoGlobalCurrentDisplayedIds, i) >= 0)
            {
                return;
            }
            _detailInfoGlobalCurrentDisplayedIds[_currentDetailInfoGlobalIdx] = i;

            var info = _pieceInfo[i].Split(",");
            _detailInfoGlobalTextTitleKana[_currentDetailInfoGlobalIdx].text = info[INFO_IDX_NAME_KANA];
            _detailInfoGlobalTextTitle[_currentDetailInfoGlobalIdx].text = info[INFO_IDX_NAME];
            var group = !string.IsNullOrEmpty(info[INFO_IDX_GROUP]) && info[INFO_IDX_GROUP] != "-" ? $"{info[INFO_IDX_GROUP]}\n" : string.Empty;
            var area = $"面積: {info[INFO_IDX_AREA]}{info[INFO_IDX_AREA_UNIT]}";
            var population = GetPopulationText(info);
            _detailInfoGlobalHeaderInfoText[_currentDetailInfoGlobalIdx].text = $"{group}{area}\n{population}";
            _detailInfoGlobalHeaderDescriptionText[_currentDetailInfoGlobalIdx].text = info[INFO_IDX_DESCRIPTION].Replace("\\n", "\n");

            SetMovieInfo(_movieInfo, _movieInfoTable[i], _detailInfoGlobalMovieRoot[_currentDetailInfoGlobalIdx]);

            _currentDetailInfoGlobalIdx = (_currentDetailInfoGlobalIdx + 1) % DETAIL_INFO_GLOBAL_COUNT;
        }

        void SetMovieInfo(string[] movieTitles, string movieIdsString, Transform movieInfoRoot)
        {
            var movieIds = movieIdsString.Split(",");
            var hasMovieInfo = !string.IsNullOrEmpty(movieIdsString) && movieIds.Length > 0;
            movieInfoRoot.gameObject.SetActive(hasMovieInfo);
            if (!hasMovieInfo)
            {
                return;
            }

            var movieInfoItemRoot = movieInfoRoot.transform.Find("#Movies");
            GameObject baseItem = movieInfoItemRoot.GetChild(0).gameObject;
            for (int i = 0; i < movieInfoItemRoot.childCount; i++)
            {
                movieInfoItemRoot.GetChild(i).gameObject.SetActive(false);
            }

            for (int i = 0; i < movieIds.Length; i++)
            {
                Transform elm = null;
                if (i < movieInfoItemRoot.childCount)
                {
                    elm = movieInfoItemRoot.GetChild(i);
                }
                else
                {
                    elm = Instantiate(baseItem).transform;
                    elm.SetParent(movieInfoItemRoot, false);
                    elm.localPosition = new Vector3(0, -i * 10, 0);
                }
                elm.gameObject.SetActive(true);
                elm.Find("#Title").GetComponent<TextMeshProUGUI>().text = movieTitles[int.Parse(movieIds[i])];
            }
        }










        [UdonSynced, FieldChangeCallback(nameof(OnNetworkMessageReceived))]
        string _networkMessage;
        const string MESSAGE_PARAM_SEPARATOR = ":";
        const int MIN_MESSAGE_LENGTH = 2;

        void SendNetwork(string message)
        {
            if (_state == PuzzleSystemState.INITIALIZE)
            {
                return;
            }

            if (Networking.LocalPlayer != null && !Networking.IsOwner(Networking.LocalPlayer, gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }

            _networkMessage = message;

#if DEBUG_FLAG
            Debug.Log(_networkMessage);
#endif

            RequestSerialization();
        }

        string OnNetworkMessageReceived
        {
            set
            {
                if (_state == PuzzleSystemState.INITIALIZE)
                {
                    return;
                }

                _networkMessage = value;

                if (string.IsNullOrEmpty(_networkMessage))
                {
                    return;
                }

#if DEBUG_FLAG
                Debug.Log($"OnNetworkMessageReceived: {_networkMessage}");
#endif

                var messages = value.Split(MESSAGE_PARAM_SEPARATOR);

                if (messages.Length < MIN_MESSAGE_LENGTH)
                {
                    return;
                }

                if (!int.TryParse(messages[1], out var typeInd))
                {
                    return;
                }

                var res = false;
                switch ((MessageType)typeInd)
                {
                    case MessageType.REQUEST_SYNC_STATE:
                        res = OnNetworkMessageReceived_RequestSyncState(messages);
                        break;
                    case MessageType.SYNC_STATE:
                        res = OnNetworkMessageReceived_SyncState(messages);
                        break;
                    case MessageType.START_GAME:
                        res = OnNetworkMessageReceived_StartGame(messages);
                        break;
                    case MessageType.CLEAR_GAME:
                        res = OnNetworkMessageReceived_ClearGame(messages);
                        break;
                    case MessageType.RESET:
                        res = OnNetworkMessageReceived_Reset(messages);
                        break;
                    case MessageType.PIECE_CORRECT:
                        res = OnNetworkMessageReceived_PieceCorrect(messages);
                        break;
                    case MessageType.CHANGE_DIFFICULTY:
                        res = OnNetworkMessageReceived_ChangeDifficulty(messages);
                        break;
                    case MessageType.CHANGE_TARGET_AREA:
                        res = OnNetworkMessageReceived_ChangeTargetArea(messages);
                        break;
                }
                if (!res)
                {
#if DEBUG_FLAG
                    Debug.Log($"Failed NetworkMessageReceiving: {(MessageType)typeInd}");
#endif
                }
            }
        }

        string MessageTypeToString(MessageType type)
        {
            return ((int)type).ToString();
        }

        bool _syncRequested = false;

        void SendNetwork_RequestSyncState()
        {
            if (Networking.LocalPlayer.isMaster)
            {
                return;
            }

#if DEBUG_FLAG
            Debug.Log($"SendNetwork_RequestSyncState");
#endif

            _syncRequested = true;
            SendNetwork(
                string.Join(MESSAGE_PARAM_SEPARATOR,
                    new string[]
                    {
                        NetworkMessageUtility.GetRandomMessageIdAsString(),
                        MessageTypeToString(MessageType.REQUEST_SYNC_STATE),
                    }
                )
            );
        }

        bool OnNetworkMessageReceived_RequestSyncState(string[] messages)
        {
            if (Networking.LocalPlayer.isMaster)
            {
                SendNetwork_SyncState();
            }

            return true;
        }

        const int SYNC_STATE_MESSAGE_LENGTH = 9;
        void SendNetwork_SyncState()
        {
#if DEBUG_FLAG
            Debug.Log($"SendNetwork_SyncState");
#endif
            SendNetwork(
                string.Join(MESSAGE_PARAM_SEPARATOR,
                    new string[]
                    {
                        NetworkMessageUtility.GetRandomMessageIdAsString(),
                        MessageTypeToString(MessageType.SYNC_STATE),
                        ((int)_state).ToString(),
                        ((int)_gameState).ToString(),
                        _currentDifficulty.ToString(),
                        _currentTargetArea.ToString(),
                        NetworkMessageUtility.EncodeFloat(GetTime()).ToString(),
                        NetworkMessageUtility.BoolArrayToString(_isOnRightPositions),
                        BestScoresToString(),
                    }
                )
            );
        }

        string BestScoresToString()
        {
            var res = string.Empty;
            for (int j = 0; j < (int)GameTargetArea.__MAX__; j++)
            {
                for (int i = 0; i < (int)GameDifficulty.__MAX__; i++)
                {
                    if (_bestGameClearTimes[j][i] == float.MaxValue)
                    {
                        continue;
                    }

                    var str = $"{i},{j},{_bestGameClearTimes[j][i]}";
                    if (string.IsNullOrEmpty(res))
                    {
                        res = $"{str}";
                    }
                    else
                    {
                        res += $",{str}";
                    }
                }
            }
            return res;
        }

        void StringToBestScores(string bestScoresString)
        {
            if (string.IsNullOrEmpty(bestScoresString))
            {
                return;
            }

            var bestScoresStrings = bestScoresString.Split(",");

            if (bestScoresStrings.Length % 3 != 0)
            {
                return;
            }

            for (int idx = 0; idx < bestScoresStrings.Length; idx += 3)
            {
                if (int.TryParse(bestScoresStrings[idx], out var i) && int.TryParse(bestScoresStrings[idx + 1], out var j) && float.TryParse(bestScoresStrings[idx + 2], out var score))
                {
                    _bestGameClearTimes[j][i] = score;
                    UpdateBestScoreUI((GameDifficulty)i, (GameTargetArea)j, score);
                }
            }
        }

        bool OnNetworkMessageReceived_SyncState(string[] messages)
        {
            if (!_syncRequested)
            {
                return true;
            }
            _syncRequested = true;

            if (messages.Length != SYNC_STATE_MESSAGE_LENGTH)
            {
                return false;
            }

            if (!int.TryParse(messages[2], out var stateInt))
            {
                return false;
            }

            if (!int.TryParse(messages[3], out var gameStateInt))
            {
                return false;
            }

            if (!int.TryParse(messages[4], out var difficultyInt))
            {
                return false;
            }

            if (!int.TryParse(messages[5], out var targetAreaInt))
            {
                return false;
            }

            if (!int.TryParse(messages[6], out var timeDiffEncodedInt))
            {
                return false;
            }

            _currentDifficulty = (GameDifficulty)difficultyInt;
            _currentTargetArea = (GameTargetArea)targetAreaInt;
            SetDifficultyUI(_currentDifficulty);
            SetTargetAreaUI(_currentTargetArea);

            _state = (PuzzleSystemState)stateInt;
            _gameState = (GameState)gameStateInt;

            if (IsInGame())
            {
                _startTime = Time.time - NetworkMessageUtility.DecodeFloat(timeDiffEncodedInt);

                var flags = NetworkMessageUtility.StringToBoolArray(messages[7]);
                if (flags.Length == _isOnRightPositions.Length)
                {
                    _isOnRightPositions = flags;
                }

                SetEnablePickupAll(false);

                switch (_currentDifficulty)
                {
                    case GameDifficulty.DIFFICULT:
                        {
                            ClearPiecePickupInteractionText();
                            SwitchPieceUIAll(PieceUIMode.SIMPLE, showSimpleKana: false, hide: true);
                            break;
                        }
                    default:
                        {
                            SwitchPieceUIAll(PieceUIMode.SIMPLE);
                            break;
                        }
                }

                SetDifficultyButtonInteractable(false);
                SetAreaButtonInteractable(false);

                SetEnablePickupWithTargetState();
                SetPieceColorWithTargetState();

                for (int i = 0; i < _isOnRightPositions.Length; i++)
                {
                    if (_isOnRightPositions[i])
                    {
                        SetEnablePickup(_piecePickups[i], false);
                        SwitchPieceUI(i, PieceUIMode.SIMPLE);
                        SetPieceColorDefault(i);
                    }
                }
            }

            StringToBestScores(messages[8]);

            return true;
        }

        const int START_GAME_MESSAGE_LENGTH = 4;
        void SendNetwork_StartGame(GameDifficulty difficulty, GameTargetArea area)
        {
#if DEBUG_FLAG
            Debug.Log($"SendNetwork_StartGame");
#endif
            SendNetwork(
                string.Join(MESSAGE_PARAM_SEPARATOR,
                    new string[]
                    {
                        NetworkMessageUtility.GetRandomMessageIdAsString(),
                        MessageTypeToString(MessageType.START_GAME),
                        ((int)difficulty).ToString(),
                        ((int)area).ToString(),
                    }
                )
            );
        }

        bool OnNetworkMessageReceived_StartGame(string[] messages)
        {
            if (messages.Length != START_GAME_MESSAGE_LENGTH)
            {
                return false;
            }

            if (!int.TryParse(messages[2], out var difficultyInt))
            {
                return false;
            }

            if (!int.TryParse(messages[3], out var targetAreaInt))
            {
                return false;
            }

            StartGameStartCountdown(false, (GameDifficulty)difficultyInt, (GameTargetArea)targetAreaInt);

            return true;
        }

        const int CLEAR_GAME_MESSAGE_LENGTH = 5;
        void SendNetwork_ClearGame(float clearTime, bool isBest, int i)
        {
#if DEBUG_FLAG
            Debug.Log($"SendNetwork_ClearGame");
#endif
            SendNetwork(
                string.Join(MESSAGE_PARAM_SEPARATOR,
                    new string[]
                    {
                        NetworkMessageUtility.GetRandomMessageIdAsString(),
                        MessageTypeToString(MessageType.CLEAR_GAME),
                        clearTime.ToString(),
                        NetworkMessageUtility.BoolToInt(isBest).ToString(),
                        i.ToString(),
                    }
                )
            );
        }

        bool OnNetworkMessageReceived_ClearGame(string[] messages)
        {
            if (messages.Length != CLEAR_GAME_MESSAGE_LENGTH)
            {
                return false;
            }

            if (!float.TryParse(messages[2], out var clearTime))
            {
                return false;
            }

            if (!int.TryParse(messages[3], out var isBestInt))
            {
                return false;
            }
            var isBest = NetworkMessageUtility.IntToBool(isBestInt);

            if (!int.TryParse(messages[4], out var pieceId))
            {
                return false;
            }

            OnPieceSetOnRightPosition(pieceId, false);
            OnClearGame(clearTime, isBest);

            return true;
        }

        void SendNetwork_Reset()
        {
#if DEBUG_FLAG
            Debug.Log($"SendNetwork_Reset");
#endif
            SendNetwork(
                string.Join(MESSAGE_PARAM_SEPARATOR,
                    new string[]
                    {
                        NetworkMessageUtility.GetRandomMessageIdAsString(),
                        MessageTypeToString(MessageType.RESET),
                    }
                )
            );
        }

        bool OnNetworkMessageReceived_Reset(string[] messages)
        {
            ResetAll();

            return true;
        }

        const int PIECE_CORRECT_MESSAGE_LENGTH = 3;
        void SendNetwork_PieceCorrect(int i)
        {
#if DEBUG_FLAG
            Debug.Log($"SendNetwork_PieceCorrect");
#endif
            SendNetwork(
                string.Join(MESSAGE_PARAM_SEPARATOR,
                    new string[]
                    {
                        NetworkMessageUtility.GetRandomMessageIdAsString(),
                        MessageTypeToString(MessageType.PIECE_CORRECT),
                        i.ToString(),
                    }
                )
            );
        }

        bool OnNetworkMessageReceived_PieceCorrect(string[] messages)
        {
            if (messages.Length != PIECE_CORRECT_MESSAGE_LENGTH)
            {
                return false;
            }

            if (!int.TryParse(messages[2], out var pieceId))
            {
                return false;
            }

            OnPieceSetOnRightPosition(pieceId, false);

            return true;
        }

        const int CHANGE_DIFFICULTY_MESSAGE_LENGTH = 3;
        void SendNetwork_ChangeDifficulty(GameDifficulty difficulty)
        {
#if DEBUG_FLAG
            Debug.Log($"SendNetwork_ChangeDifficulty");
#endif
            SendNetwork(
                string.Join(MESSAGE_PARAM_SEPARATOR,
                    new string[]
                    {
                        NetworkMessageUtility.GetRandomMessageIdAsString(),
                        MessageTypeToString(MessageType.CHANGE_DIFFICULTY),
                        ((int)difficulty).ToString(),
                    }
                )
            );
        }

        bool OnNetworkMessageReceived_ChangeDifficulty(string[] messages)
        {
            if (messages.Length != CHANGE_DIFFICULTY_MESSAGE_LENGTH)
            {
                return false;
            }

            if (!int.TryParse(messages[2], out var difficultyInt))
            {
                return false;
            }

            SetDifficultyUI((GameDifficulty)difficultyInt);

            return true;
        }

        const int CHANGE_TARGET_AREA_MESSAGE_LENGTH = 3;
        void SendNetwork_ChangeTargetArea(GameTargetArea area)
        {
#if DEBUG_FLAG
            Debug.Log($"SendNetwork_ChangeTargetArea");
#endif
            SendNetwork(
                string.Join(MESSAGE_PARAM_SEPARATOR,
                    new string[]
                    {
                        NetworkMessageUtility.GetRandomMessageIdAsString(),
                        MessageTypeToString(MessageType.CHANGE_TARGET_AREA),
                        ((int)area).ToString(),
                    }
                )
            );
        }

        bool OnNetworkMessageReceived_ChangeTargetArea(string[] messages)
        {
            if (messages.Length != CHANGE_TARGET_AREA_MESSAGE_LENGTH)
            {
                return false;
            }

            if (!int.TryParse(messages[2], out var targetAreaInt))
            {
                return false;
            }

            SetTargetAreaUI((GameTargetArea)targetAreaInt);

            return true;
        }
    }

    public static class NetworkMessageUtility
    {
        const int NETWORK_ENCODE_FLOAT_TO_INT_DIGIT = 10000;

        public static int EncodeFloat(float v)
        {
            return (int)(v * NETWORK_ENCODE_FLOAT_TO_INT_DIGIT);
        }

        public static float DecodeFloat(int v)
        {
            return v / (float)NETWORK_ENCODE_FLOAT_TO_INT_DIGIT;
        }

        public static string EncodeVector3(ref Vector3 v)
        {
            return $"{EncodeFloat(v.x)},{EncodeFloat(v.y)},{EncodeFloat(v.z)}";
        }

        public static Vector3 DecodeVector3(int x, int y, int z)
        {
            return new Vector3(DecodeFloat(x), DecodeFloat(y), DecodeFloat(z));
        }

        public static int GetRandomSeed()
        {
            if (Networking.LocalPlayer == null)
            {
                return DateTime.Now.Millisecond;
            }
            return DateTime.Now.Millisecond + Networking.LocalPlayer.playerId * 1000;
        }

        public static int GetRandomMessageId()
        {
            return Random.Range(0, 9999);
        }

        public static string GetRandomMessageIdAsString()
        {
            return GetRandomMessageId().ToString();
        }

        public static int BoolToInt(bool v)
        {
            return v ? 1 : 0;
        }

        public static bool IntToBool(int v)
        {
            return v == 1;
        }

        public static string BoolArrayToString(bool[] flags)
        {
            string res = string.Empty;
            for (int i = 0; i < flags.Length; i++)
            {
                res += BoolToInt(flags[i]).ToString();
            }
            return res;
        }

        public static bool[] StringToBoolArray(string flagsString)
        {
            var fs = flagsString.ToCharArray();
            var res = new bool[fs.Length];
            for (int i = 0; i < fs.Length; i++)
            {
                if (int.TryParse(fs[i].ToString(), out var flagInt))
                {
                    res[i] = IntToBool(flagInt);
                }
            }
            return res;
        }
    }
}

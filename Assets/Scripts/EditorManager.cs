using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public enum EditorMode
{
    Idle, Place, Select, PropertyChange, MultiPlace
}

public class EditorManager : MonoBehaviour
{
    public static EditorManager Instance;

    public LayerMask gridLayer; // 바닥(그리드) 레이어
    public float gridSize = 1f; // 정수 좌표 맞춤용

    private BlockData currentBlock;
    private GameObject previewObject;
    private Quaternion currentRotation = Quaternion.identity;

    // 블록 위치 관리용
    public Dictionary<Vector3Int, GameObject> placedBlocks = new Dictionary<Vector3Int, GameObject>();

    private GameObject selectedObject;
    private GameObject selectedPropChangeObject;


    //선택 인디케이터 관련
    [SerializeField] GameObject selectIndicatorPrefab;
    GameObject selectIndicator;

    public CameraController cameraController;

    //이 에디터의 모드(상태패턴 적용?)
    EditorMode editorMode;
    public EditorMode EditorMode { get => editorMode; set => ChangeMode(value); }

    private float clickPressedTime;
    public Vector3Int lastPlacedPos;

    void Awake() => Instance = this;

    
    private void ChangeMode(EditorMode mode)
    {
        if (EditorMode == mode) return;
        Debug.Log($"에디터모드 상태 변경 감지: [{editorMode}] => [{mode}]");
        //여기엔 현재 상태에 따라 상태를 나갈 때 수행할 부분을 체크.
        switch (editorMode)
        {
            case EditorMode.Idle:
                break;
            case EditorMode.Place:
                if (mode == EditorMode.Idle)
                {
                    CancelPlacement();
                }
                break;
            case EditorMode.Select:
                if (mode == EditorMode.Idle || mode == EditorMode.Place)
                {
                    Deselect();
                }
                if (mode == EditorMode.PropertyChange)
                {
                    //프로퍼티체인지핸들러 객체 만들고 selectedobject로 init
                    PropertyDragHandler propHandler;
                    if (selectedObject.TryGetComponent<IOptionalProperty>(out IOptionalProperty optionalProp))
                    {
                        propHandler = selectedObject.AddComponent<PropertyDragHandler>();
                        propHandler.Init(optionalProp);
                        selectedPropChangeObject = selectedObject;
                        //곧이어 Deselect();
                        Deselect();
                    }
                    else mode = EditorMode.Select; //IOptionalProperty를 구현하지 않은 객체를 선택한 상황이므로 PropertyChange로의 전환을 막음.
                }
                break;
            case EditorMode.PropertyChange:
                //에디터모드가 어떻게 변할 예정이든 상관없이 프로퍼티체인지핸들러 객체 삭제
                if (selectedPropChangeObject.TryGetComponent<PropertyDragHandler>(out var handler))
                {
                    Destroy(handler);
                }
                selectedPropChangeObject = null;
                break;
            default:
                break;
                
        }
        editorMode = mode;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            //CancelPlacement();
            Deselect();
            EditorMode = EditorMode.Idle;
            //현재 상태와 무관하게 Esc를 누르면 에디터모드는 Idle로 돌아감
        }

        //editorMode에 따라 할 수 있는 일들과 할 수 없는 일들을 몰아넣음.
        if (EditorMode == EditorMode.Idle)
        {
            if (Input.GetMouseButtonDown(00) || Input.GetMouseButtonDown(01))
            {
                //이때 레이를 쏨.
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 100f, gridLayer))
                {
                    //좌클릭일때 -> 선택
                    if (Input.GetMouseButtonDown(00) && !hit.collider.CompareTag("Ground"))
                    {
                        Select(hit.collider.gameObject);
                        return;
                    }
                    //우클릭일때 -> 삭제
                    if (Input.GetMouseButtonDown(01))
                    {
                        RemoveBlock(hit);
                    }
                }
                else
                {
                    
                }
           
            }
        }
        if (EditorMode == EditorMode.Place)
        {
            //TODO: 여기에, 블록 다중생성 기능 추가 필요.
            bool isValid = HandlePlaceMode(out Vector3 placementPos, out bool canPlace);
            //R -> 돌리기(배치가능 조건을 통과해야만 함)
            if (Input.GetKeyDown(KeyCode.R))
            {
                if (previewObject)
                {
                    previewObject.transform.Rotate(0, 90, 0);
                    currentRotation = previewObject.transform.rotation;
                }
            }
            if (!isValid)
            {
                return;
            }
            //좌클릭일때 -> 배치(배치가능 조건을 통과해야만 함)
            if (Input.GetMouseButtonDown(0))
            {
                clickPressedTime = 0;
                currentRotation = previewObject.transform.rotation;
                if (currentBlock && canPlace)
                    PlaceBlock(placementPos, currentRotation);
            }
            // 미리보기 위치 갱신
            if (previewObject)
                previewObject.transform.position = placementPos;

            if (Input.GetMouseButton(0))         
                clickPressedTime += Time.deltaTime;
            
            if (Input.GetMouseButtonUp(0))            
                clickPressedTime = 0;
            

            //플레이스 모드일 때 클릭타임이 길어지면
            if (clickPressedTime >= .2f)
            {
                EditorMode = EditorMode.MultiPlace;
            }

        }
        if (EditorMode == EditorMode.Select)
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                if (selectedObject)
                    RotateSelectedObject();
            }
            if (Input.GetKeyDown(KeyCode.Delete))
            {
                RemoveBlock(selectedObject);
            }

            if (Input.GetKeyDown(KeyCode.Space) && selectedObject)
            {
                EditorMode = EditorMode.PropertyChange;
            }
            if (Input.GetMouseButtonDown(00) || Input.GetMouseButtonDown(01))
            {
                //이때 레이를 쏨.
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

                if (Physics.Raycast(ray, out RaycastHit hit, 100f, gridLayer))
                {
                    //좌클릭일때 -> 선택하거나
                    if (Input.GetMouseButtonDown(00) && !hit.collider.CompareTag("Ground"))
                    {
                        Select(hit.collider.gameObject);
                    } 
                   
                    else
                    {
                        Deselect();
                        EditorMode = EditorMode.Idle;
                    }

                    //우클릭일때 -> 삭제
                    if (Input.GetMouseButtonDown(01))
                    {
                        RemoveBlock(hit);
                        
                    }
                }

            }
        }
        if (EditorMode == EditorMode.PropertyChange)
        {
            //오늘할일
        }
        if (EditorMode == EditorMode.MultiPlace)
        {
            if (Input.GetMouseButtonUp(0))
                //마우스 좌클릭을 떼면 MultiPlace 모드를 종료하고 Place모드로 복귀
            {
                clickPressedTime = 0;
                EditorMode = EditorMode.Place;
                return;
            }

            if (Input.GetMouseButton(0))
            {
                //좌클릭을 하고 있는 동안
                bool isValid = HandlePlaceMode(out Vector3 placementPos, out bool canPlace);
                if (!isValid)
                {
                    //유효한 곳이 아니면 리턴
                    return;
                }

                //좌클릭일때 -> 배치(배치가능 조건을 통과해야만 함)

                if (currentBlock && canPlace && (placementPos != lastPlacedPos)) {
                    if (lastPlacedPos.y == placementPos.y)
                        PlaceBlock(placementPos, currentRotation);
                }
                // 미리보기 위치 갱신
                if (previewObject)
                    previewObject.transform.position = placementPos;

            }
        }


        

        
    }

    private void RotateSelectedObject()
    {
        RotateCommand cmd = new(selectedObject);
        selectedObject.transform.Rotate(0, 90, 0);
        cmd.rotateRotation = selectedObject.transform.rotation;
        CommandManager.Instance.AddCommand(cmd);
        cmd.Execute();
    }

    private bool HandlePlaceMode(out Vector3 placementPos, out bool canPlace)
    {
        //계속 레이 쏘기
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f, gridLayer);

        if (hits.Length == 0)
        {
            placementPos = Vector3.zero;
            canPlace = false;
            return false;
        }

        // 거리가 가까운 순으로 정렬
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        RaycastHit? hitResult = null;

        foreach (var hitt in hits)
        {
            if (previewObject != null && hitt.collider.gameObject == previewObject)
                continue; // 프리뷰오브젝트는 무시
            hitResult = hitt;
            break;
        }

        if (!hitResult.HasValue)
        {
            placementPos = Vector3.zero;
            canPlace = false;
            return false;
        }


        RaycastHit hit = hitResult.Value;

        // 클릭한 표면의 법선 방향으로 한 칸 위에 기즈모 표시
        Vector3 targetPos = hit.point + hit.normal * (gridSize * 0.5f);
        placementPos = SnapToGrid(targetPos);

        // 설치 가능 여부 판단
        Vector3Int gridPos = Vector3Int.RoundToInt(placementPos);

        canPlace = !placedBlocks.ContainsKey(gridPos);
        SetPreviewColor(canPlace);
        return true;
    }



    // 블록 선택: 배치할 블록 이야기임.
    public void ChooseBlock(BlockData data)
    {
        Deselect();
        CancelPlacement();
        currentBlock = data;
        previewObject = Instantiate(data.prefab);
        SetPreviewMaterial(previewObject);
    }

    // 선택: 게임오브젝트 단순 선택
    public void Select(GameObject obj)
    {
        if (!obj)
        {
            Deselect();
            return;
        }
        selectedObject = obj;
        if (!selectedObject.GetComponent<BlockDragHandler>())
            selectedObject.AddComponent<BlockDragHandler>();

        if (selectedObject.CompareTag("FlowWaterBlock")) //선택된 오브젝트의 태그가 물 블록일때 한정으로
        {
            selectedObject.AddComponent<FlowWaterBlock>();
        }
        
        //선택되었다는 인디케이터 보여주기
        EditorMode = EditorMode.Select;

        SetIndicator(true);
        //cameraMovementObject?.SetActive(false);
    }

    public void SetIndicator(bool isVisible)
    {
        if (!selectIndicator)
        {
            var go = Instantiate(selectIndicatorPrefab);
            selectIndicator = go;
        }
        //카메라 움직임 해제해주기
        selectIndicator.SetActive(isVisible);
        selectIndicator.transform.position = selectedObject.transform.position + Vector3.up;
    }
    // 선택해제: 게임오브젝트 단순 선택해제
    public void Deselect()
    {
        //선택되었다는 인디케이터 제거
        //돌려줘요 내 카메라
        if (selectedObject)
        {
            SetIndicator(false);
            Destroy(selectedObject.GetComponent<BlockDragHandler>());
            if (selectedObject.CompareTag("FlowWaterBlock")) //선택된 오브젝트의 태그가 물 블록일때 한정으로
            {
            Destroy(selectedObject.GetComponent<FlowWaterBlock>());
            }
            
        }

        selectedObject = null;
        //cameraMovementObject?.SetActive(true);
    }

    // 블록 설치
    public void PlaceBlock(Vector3 worldPos, Quaternion worldRot)
    {
        // 좌표를 정수 그리드 단위로 변환 (블록 단위)
        Vector3Int gridPos = Vector3Int.RoundToInt(worldPos);

        // 이미 블록이 있으면 설치 안 함
        if (placedBlocks.ContainsKey(gridPos))
        {
            Debug.Log("이미 블록이 있습니다!");
            return;
        }

        //// 블록 생성
        //GameObject block = BlockFactory.Instance.CreateBlock(currentBlock, gridPos);

        //// 딕셔너리에 등록
        //placedBlocks.Add(gridPos, block);

        // 커맨드 생성
        var cmd = new PlaceCommand(currentBlock, gridPos, worldRot, Vector3.one);

        // 커맨드 등록
        CommandManager.Instance.AddCommand(cmd);
        
        // 커맨드 수행
        cmd.Execute();

        //// 블록목록에 등록: 커맨드에서 처리됨.
        //placedBlocks.Add(gridPos, cmd.Target);
        lastPlacedPos = gridPos;
    }

    public void RemoveBlock(GameObject obj)
    {
        if (!obj) return;
        //obj가 null이거나 미싱이면 리턴
        // object의 정확한 int포지션 확인
        Vector3Int gridPos = Vector3Int.RoundToInt(obj.transform.position);

        //블록 전체 리스트의 gridPos키의 value가 없으면 return;(해당 위치에 블록이 없으면 걍 리턴)
        if (!placedBlocks.ContainsKey(gridPos)) return;

        
        var cmd = new RemoveCommand(obj, new((int)obj.transform.position.x, (int)obj.transform.position.y, (int)obj.transform.position.z), obj.transform.rotation, obj.transform.transform.localScale);

        //커맨드 등록
        CommandManager.Instance.AddCommand(cmd);

        //커맨드 수행
        cmd.Execute();
        Deselect();
        EditorMode = EditorMode.Idle;
    }
    // 블록 제거
    public void RemoveBlock(RaycastHit hit)
    {
        if (hit.collider.CompareTag("Ground")) return;
        RemoveBlock(hit.collider.gameObject);
        //// 정확히 클릭한 블록 제거
        //Vector3Int gridPos = Vector3Int.RoundToInt(hit.collider.transform.position);

        ////블록 전체 리스트의 gridPos키의 value가 없으면 return;(해당 위치에 블록이 없으면 걍 리턴)
        //if (!placedBlocks.ContainsKey(gridPos)) return;
        
        ////커맨드 생성을 위해 타겟 정의
        //GameObject removeTarget = placedBlocks[gridPos];

        ////코드치기 편하게 하려고 타겟의 트랜스폼 정의
        //Transform tTransform = removeTarget.transform;
        //var cmd = new RemoveCommand(removeTarget, new((int)tTransform.position.x, (int)tTransform.position.y, (int)tTransform.position.z), tTransform.rotation, removeTarget.transform.localScale);

        ////커맨드 등록
        //CommandManager.Instance.AddCommand(cmd);

        ////커맨드 수행
        //cmd.Execute();
    }

    // 배치 취소
    void CancelPlacement()
    {
        currentBlock = null;
        if (previewObject != null) Destroy(previewObject);
    }

    // 그리드 스냅
    public Vector3 SnapToGrid(Vector3 pos)
    {
        pos.x = Mathf.Round(pos.x / gridSize) * gridSize;
        pos.y = Mathf.Round(pos.y / gridSize) * gridSize;
        pos.z = Mathf.Round(pos.z / gridSize) * gridSize;
        return pos;
    }

    // 미리보기 색상 설정
    void SetPreviewMaterial(GameObject obj)
    {
        foreach (var rend in obj.GetComponentsInChildren<Renderer>())
        {
            rend.material.color = new Color(1, 1, 1, 0.5f); // 반투명
        }
    }

    // 기즈모 색상 변경 (배치 가능/불가 표시)
    void SetPreviewColor(bool canPlace)
    {
        if (previewObject == null) return;

        Color color = canPlace ? new Color(1, 1, 1, 0.5f) : new Color(1, 0, 0, 0.5f);
        foreach (var rend in previewObject.GetComponentsInChildren<Renderer>())
            rend.material.color = color;
    }
    public Dictionary<Vector3Int, GameObject> GetPlacedBlocks() => placedBlocks;

    public void SetPlacedBlocks(Dictionary<Vector3Int, GameObject> newData)
    {
        foreach (var b in placedBlocks.Values)
            Destroy(b);
        placedBlocks.Clear();

        placedBlocks = newData;
    }
}
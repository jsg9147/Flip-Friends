using Mirror;
using UnityEngine;

// 맵 데이터의 배치 항목 하나를 팔레트 프리팹으로 만들어 서버에 Spawn한다.
public sealed class PlacedObjectFactory
{
    private readonly MapEditorPalette palette;

    public PlacedObjectFactory(MapEditorPalette palette)
    {
        this.palette = palette;
    }

    public GameObject Spawn(PlacedObjectData objData)
    {
        GameObject prefab = palette.GetPrefab(objData.prefabID);
        if (prefab == null)
            return null;

        Vector3 position = objData.position.ToVector3();
        Quaternion rotation = Quaternion.Euler(0f, 0f, objData.rotation);

        GameObject obj = Object.Instantiate(prefab, position, rotation);
        obj.transform.localScale = objData.scale.ToVector3();

        if (obj.GetComponent<NetworkIdentity>() == null)
        {
            Debug.LogError($"팔레트 프리팹에 NetworkIdentity가 없습니다: {objData.prefabID}", obj);
            Object.Destroy(obj);
            return null;
        }

        NetworkServer.Spawn(obj);
        return obj;
    }
}

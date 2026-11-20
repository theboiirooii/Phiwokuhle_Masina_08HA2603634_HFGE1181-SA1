using UnityEngine;

public class FoodSizeHandler : SizeHandler
{
    private void OnDestroy()
    {
        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.CleanFoodArray(this);
        }
    }
}

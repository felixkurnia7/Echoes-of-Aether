using UnityEngine;

public class BootstrapTest : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private EnemyData testEnemy;
    void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current.bKey.wasPressedThisFrame)
            gameManager.StartBattle(testEnemy);
    }
}

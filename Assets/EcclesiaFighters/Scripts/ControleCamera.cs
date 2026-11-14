using UnityEngine;

public class ControleCamera : MonoBehaviour
{
    public float sensibilidadeMouse = 100f;
    public Transform corpoJogador;

    private float rotacaoX = 0f;

    void Start()
    {
        // Esconde a seta do mouse e a prende no centro da tela
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // Pega a movimentação do mouse
        float mouseX = Input.GetAxis("Mouse X") * sensibilidadeMouse * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * sensibilidadeMouse * Time.deltaTime;

        // Calcula a rotação vertical (olhar para cima/baixo) e limita o ângulo
        rotacaoX -= mouseY;
        rotacaoX = Mathf.Clamp(rotacaoX, -90f, 90f);

        // Aplica a rotação na câmera (cima/baixo) e no corpo do jogador (esquerda/direita)
        transform.localRotation = Quaternion.Euler(rotacaoX, 0f, 0f);
        corpoJogador.Rotate(Vector3.up * mouseX);
    }
}

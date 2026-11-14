using UnityEngine;

public class MovimentoJogador1 : MonoBehaviour
{
    public CharacterController controller;

    [Header("Configurações de Velocidade")]
    public float velocidadeAndar = 5f;
    public float velocidadeCorrer = 9f;
    public float forcaPulo = 3f;
    
    [Header("Física")]
    public float gravidade = -15f; // Aumentada para um pulo menos "flutuante"

    private float velocidadeAtual;
    private Vector3 velocidadeVertical;

    void Update()
    {
        // 1. Alternar entre Andar e Correr (Segurando Shift)
        if (Input.GetKey(KeyCode.LeftShift))
        {
            velocidadeAtual = velocidadeCorrer;
        }
        else
        {
            velocidadeAtual = velocidadeAndar;
        }

// 2. Calcula o movimento WASD / Setas (sem aplicar o Move ainda)
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 mover = (transform.right * x + transform.forward * z) * velocidadeAtual;

        // 3. Sistema de Pulo e Gravidade
        if (controller.isGrounded)
        {
            if (velocidadeVertical.y < 0)
            {
                velocidadeVertical.y = -2f; // Mantém o jogador firme no chão
            }

            // Se pressionar Espaço e estiver no chão, pula
            if (Input.GetButtonDown("Jump"))
            {
                // Fórmula física para altura do pulo estavel: sqrt(h * -2 * g)
                velocidadeVertical.y = Mathf.Sqrt(forcaPulo * -2f * gravidade);
            }
        }

        // Aplica gravidade continuamente
        velocidadeVertical.y += gravidade * Time.deltaTime;

        // 4. Combina os eixos e chama o Move EXATAMENTE UMA VEZ
        Vector3 movimentoFinal = mover + velocidadeVertical;
        controller.Move(movimentoFinal * Time.deltaTime);
    }
}

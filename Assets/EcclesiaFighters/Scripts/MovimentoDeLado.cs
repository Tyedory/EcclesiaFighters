using UnityEngine;

public class MovimentoDeLado : MonoBehaviour
{
    public CharacterController controller;

    [Header("Configurações de Velocidade")]
    public float velocidadeAndar = 5f;
    public float velocidadeCorrer = 9f;
    public float forcaPulo = 3f;
    
    [Header("Física")]
    public float gravidade = -15f; 

    private float velocidadeAtual;
    private Vector3 velocidadeVertical;

    void Update()
    {
        // 1. Alternar entre Andar e Correr
        if (Input.GetKey(KeyCode.LeftShift))
        {
            velocidadeAtual = velocidadeCorrer;
        }
        else
        {
            velocidadeAtual = velocidadeAndar;
        }

// 2. Calcula o movimento horizontal (sem aplicar o Move ainda)
        float inputAD = Input.GetAxis("Horizontal");
        Vector3 mover = Vector3.forward * inputAD * velocidadeAtual;

// 3. Sistema de Pulo e Gravidade
        if (controller.isGrounded)
        {
            if (velocidadeVertical.y < 0)
            {
                velocidadeVertical.y = -2f; 
            }

            if (Input.GetButtonDown("Jump"))
            {
                velocidadeVertical.y = Mathf.Sqrt(forcaPulo * -2f * gravidade);
            }
        }

        // Aplica a gravidade na vertical
        velocidadeVertical.y += gravidade * Time.deltaTime;

        // 4. Combina os eixos e chama o Move EXATAMENTE UMA VEZ
        Vector3 movimentoFinal = mover + velocidadeVertical;
        controller.Move(movimentoFinal * Time.deltaTime);

    }
}
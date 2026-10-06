import { useEffect, useState } from 'react'
import './App.css'

function App() {
  const [name, setName] = useState("");
  const [motive, setMotive] = useState("");
  const [priority, setPriority] = useState(false);

  const [fila, setFila] = useState (() => {
    const filaSalva = localStorage.getItem("fila-atendimento");

    return filaSalva ? JSON.parse(filaSalva) : [];
  });

  useEffect (() => {
    localStorage.setItem("fila-atendimento", JSON.stringify(fila));
  }, [fila]);

  function gerarSenha() {
    const numero = fila.lenght + 1;

    return priority 
    ? `P${string(numero).padStart(3, "0")}`
    : `A${string(numero).padStart(3, "0")}`;
  }

  function enterFila(event) {
    event.preventDefault();

    if (!name.trim()) {
      alert("Digite seu nome!");
      return
    }

    if (!motive.trim()) {
      alert("Informe o motivo do atendimento.");
      return
    }

    const newPerson = {
      id : Date.now(),
      senha : gerarSenha(),
      nome : name.trim(),
      motivo : motive.trim(),
      prioritario,
      horario: new Date().toLocaleTimeString("pt-BR", {
        hour: "2-digit",
        minut: "2-digit"
      }),
    };

    setFila((filaAtual) => [...filaAtual, newPerson]);

    setName("");
    setMotive("");
    setPriority(false);

  }

  function removeFila(id) {
    setFila((filaAtual) => 
      filaAtual.filter((pessoa) => pessoa.id !== id)
    );
  }

  return (
    <div className="app">

      {/* CABEÇALHO*/}
      <header className="header">
        <div className="header-content">
          <div>
            <span className="instituicao">UNISAGRADO</span>

            <h1>Secretaria · Fila de atendimento</h1>
          </div>
        </div>
      </header>
      
      <main className="container">
        {/*FORMULÁRIO*/}
        <section className="card">

          <h2>Retire sua senha</h2>

          <p className="descricao">
            preencha seus dados e acompanhe a fila pelo celular
          </p>

          <form onSubmit={enterFila}>

            {/*NOME*/}
            <div className="campo">
              <label htmlFor="nome">
                Seu nome
              </label>

              <input
              id="nome"
              type="text"
              placeholder="Nome completo"
              value={name}
              onChange={(event) => setName(event.target.value)}
              />
            </div>

            {/*MOTIVO*/}
            <div className="campo">
              <label htmlFor="motivo">
                Qual o motivo do atendimento
              </label>

              <textarea
              id="motivo"
              placeholder="Ex.: solicitar hisórico escolar"
              value={motive}
              onChange={(event) => setMotivo(event.tager.value)}
              rows="4"
              />
            </div>

            {/*PRIORIDADE*/}
            <div
              className={`prioridade ${
                priority ? "prioridade-ativa" : ""
              }`}>
              <div>
                <strong>Atendimento preferencial</strong>

                <p>
                  Idosos, gestantes, pessoas com deficiência
                  ou com criança de colo.
                </p>
              </div>
              <div
                className={`switch ${
                priority ? "switch-ativo" : ""
                }`}>
                <div className="switch-bolinha"></div>
              </div>
            </div>

            {/*BOTÃO*/}
            <button type="submit" className="botao">
              Entrar na fila
            </button>

          </form>
        </section>

        {/*FILA*/}
        <section className="fila-section">
          <h2 className="titulo-fila">
            FILA DE HOJE
          </h2>

          {fila.length === 0 ? (
            <div className="fila-vazia">
              Nenhuma pessoa aguardando no momento.
            </div>
            ) : (
              <div className="fila">

                {fila.map((pessoa, index) => (
                  <div
                    className={`pessoa ${
                      pessoa.priority
                      ? "pessoa-prioritaria"
                      : ""
                    }`}
                    key={pessoa.id}>
                      
                      <div className="numero">
                    <span>{pessoa.senha}</span>
                  </div>

                  <div className="dados">
                    <strong>{pessoa.nome}</strong>

                    <span>{pessoa.motivo}</span>

                    <small>
                      Entrada: {pessoa.horario}
                    </small>

                    {pessoa.prioritario && (
                      <span className="badge">
                        Atendimento preferencial
                      </span>
                    )}
                  </div>

                  <button
                    className="remover"
                    onClick={() => removerDaFila(pessoa.id)}
                    title="Remover da fila"
                  >
                    ×
                  </button>
                </div>
    
        </section>
      </main>
  </div>
   
     
  )
};
export default App;

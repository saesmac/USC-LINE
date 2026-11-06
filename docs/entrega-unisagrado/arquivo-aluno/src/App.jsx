import { useCallback, useEffect, useRef, useState } from 'react'
import { alunoApi } from './api'
import './App.css'

const STORAGE_KEY = 'unisagrado-minha-senha-v1'
const dateString = (value) => value ? new Date(value).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' }) : ''
function readStored() {
  try {
    const data = JSON.parse(localStorage.getItem(STORAGE_KEY) || 'null')
    return data && typeof data.codigo === 'string' ? data : null
  } catch { return null }
}
function normalizeTicket(data, previous = {}) {
  const item = data?.senha && typeof data.senha === 'object' ? data.senha : data?.ticket || data || {}
  return {
    ...previous,
    codigo: item.codigo ?? previous.codigo,
    nome: item.nome ?? previous.nome,
    descricao: item.descricao ?? previous.descricao,
    preferencial: item.preferencial ?? previous.preferencial,
    status: item.status ?? previous.status ?? 'AGUARDANDO',
    posicaoFila: item.posicaoFila ?? item.posicao ?? null,
    mesaNumero: item.mesaNumero ?? item.mesa?.numero ?? item.mesa ?? previous.mesaNumero ?? null,
    criadoEm: item.criadoEm ?? item.createdAt ?? previous.criadoEm,
    chamadoEm: item.chamadoEm ?? item.calledAt ?? previous.chamadoEm,
    finalizadoEm: item.finalizadoEm ?? item.finishedAt ?? previous.finalizadoEm,
    dataFila: item.dataFila ?? previous.dataFila,
  }
}
function buzz() {
  if ('vibrate' in navigator) navigator.vibrate([250, 120, 250, 120, 400])
}

export default function App() {
  const [nome, setNome] = useState('')
  const [descricao, setDescricao] = useState('')
  const [preferencial, setPreferencial] = useState(false)
  const [ticket, setTicket] = useState(readStored)
  const [fila, setFila] = useState([])
  const [loading, setLoading] = useState(false)
  const [cancelLoading, setCancelLoading] = useState(false)
  const [error, setError] = useState('')
  const [warning, setWarning] = useState('')
  const [confirmCancel, setConfirmCancel] = useState(false)
  const lastStatus = useRef(ticket?.status)

  const refresh = useCallback(async () => {
    const current = readStored()
    try {
      const results = await Promise.allSettled([
        alunoApi.fila(),
        current?.codigo ? alunoApi.acompanhar(current.codigo) : Promise.resolve(null),
      ])
      if (results[0].status === 'fulfilled') {
        const value = results[0].value
        setFila(Array.isArray(value) ? value : Array.isArray(value?.senhas) ? value.senhas : Array.isArray(value?.tickets) ? value.tickets : [])
      }
      if (current?.codigo && results[1].status === 'fulfilled' && results[1].value) {
        const updated = normalizeTicket(results[1].value, current)
        if (updated.status === 'CHAMADO' && lastStatus.current !== 'CHAMADO') buzz()
        lastStatus.current = updated.status
        setTicket(updated)
        localStorage.setItem(STORAGE_KEY, JSON.stringify(updated))
        setWarning('')
      } else if (current?.codigo && results[1].status === 'rejected') {
        setWarning('Não foi possível atualizar sua senha agora. Tentaremos novamente automaticamente.')
      } else if (results[0].status === 'rejected') {
        setWarning('A fila está temporariamente indisponível. Tentaremos novamente.')
      }
    } catch { setWarning('Falha ao atualizar as informações.') }
  }, [])

  useEffect(() => {
    refresh()
    const interval = setInterval(() => { if (!document.hidden) refresh() }, 5000)
    const onVisible = () => { if (!document.hidden) refresh() }
    document.addEventListener('visibilitychange', onVisible)
    return () => { clearInterval(interval); document.removeEventListener('visibilitychange', onVisible) }
  }, [refresh])

  async function solicitar(event) {
    event.preventDefault()
    setError('')
    if (!nome.trim() || !descricao.trim()) { setError('Preencha seu nome e o motivo do atendimento.'); return }
    setLoading(true)
    try {
      const response = await alunoApi.solicitar({ nome: nome.trim(), descricao: descricao.trim(), preferencial })
      const created = normalizeTicket(response, { nome: nome.trim(), descricao: descricao.trim(), preferencial })
      if (!created.codigo) throw new Error('A API não retornou o código da senha. Verifique a resposta do servidor.')
      localStorage.setItem(STORAGE_KEY, JSON.stringify(created))
      setTicket(created)
      lastStatus.current = created.status
      setNome(''); setDescricao(''); setPreferencial(false)
      await refresh()
    } catch (err) { setError(err.message) }
    finally { setLoading(false) }
  }

  async function cancelar() {
    if (!ticket?.codigo) return
    setCancelLoading(true); setError('')
    try {
      await alunoApi.cancelar(ticket.codigo)
      const updated = { ...ticket, status: 'CANCELADO' }
      setTicket(updated)
      localStorage.setItem(STORAGE_KEY, JSON.stringify(updated))
      setConfirmCancel(false)
      await refresh()
    } catch (err) { setError(err.message); setConfirmCancel(false) }
    finally { setCancelLoading(false) }
  }

  function novaSenha() {
    localStorage.removeItem(STORAGE_KEY)
    setTicket(null); setError(''); setWarning(''); lastStatus.current = null
  }

  const status = ticket?.status
  const called = status === 'CHAMADO' || status === 'EM_ATENDIMENTO'
  const ended = status === 'FINALIZADO' || status === 'CANCELADO'
  const canCancel = status === 'AGUARDANDO' || status === 'CHAMADO'
  const mesa = ticket?.mesaNumero
  const displayFila = fila.filter((item) => item.status === 'AGUARDANDO' || item.status === 'CHAMADO')

  return (
    <div className="app">
      <header className="header"><div className="header-inner"><div><span className="eyebrow">UNISAGRADO</span><h1>Secretaria · Fila de atendimento</h1></div><span className="header-subtitle">Atendimento acadêmico</span></div></header>
      <main className="container">
        {error && <div role="alert" className="alert alert-error">{error}</div>}
        {warning && <div role="status" className="alert alert-warning">{warning}</div>}
        {!ticket ? (
          <section className="card form-card" aria-labelledby="form-title">
            <h2 id="form-title">Retire sua senha</h2><p className="intro">Preencha seus dados e acompanhe a fila pelo celular.</p>
            <form onSubmit={solicitar}>
              <label className="field" htmlFor="nome"><span>Seu nome</span><input id="nome" autoComplete="name" maxLength="120" placeholder="Nome completo" value={nome} onChange={(e) => setNome(e.target.value)} required /></label>
              <label className="field" htmlFor="descricao"><span>Qual o motivo do atendimento?</span><textarea id="descricao" maxLength="1000" rows="4" placeholder="Ex.: solicitar histórico escolar" value={descricao} onChange={(e) => setDescricao(e.target.value)} required /></label>
              <label className={`priority ${preferencial ? 'priority-active' : ''}`}><span><strong>Atendimento preferencial</strong><small>Idosos, gestantes, pessoas com deficiência ou com criança de colo.</small></span><input type="checkbox" role="switch" checked={preferencial} onChange={(e) => setPreferencial(e.target.checked)} /><span className="switch" aria-hidden="true"><span /></span></label>
              <button className="btn btn-primary btn-full" type="submit" disabled={loading}>{loading ? 'Gerando sua senha...' : 'Entrar na fila'}</button>
            </form>
          </section>
        ) : (
          <section className={`card ticket-card ${called ? 'ticket-called' : ''} ${ended ? 'ticket-ended' : ''}`} aria-live="polite">
            <span className="ticket-label">{called ? 'É A SUA VEZ!' : status === 'FINALIZADO' ? 'ATENDIMENTO FINALIZADO' : status === 'CANCELADO' ? 'SENHA CANCELADA' : 'SUA SENHA'}</span>
            <strong className="ticket-number">{ticket.codigo}</strong>
            <p className="ticket-message">{called ? (mesa ? `Dirija-se à mesa ${mesa}` : 'Sua senha foi chamada!') : status === 'AGUARDANDO' ? (ticket.posicaoFila === 1 ? 'Você é o próximo a ser chamado' : ticket.posicaoFila ? `Você está na posição ${ticket.posicaoFila} da fila` : 'Aguarde, você será chamado em breve') : status === 'FINALIZADO' ? 'Atendimento concluído. Obrigado!' : 'Sua senha foi cancelada.'}</p>
            <p className="ticket-meta">{ticket.nome ? `${ticket.nome} · ` : ''}{ticket.criadoEm ? `senha retirada às ${dateString(ticket.criadoEm)}` : 'Acompanhe as atualizações nesta página'}</p>
            {status === 'EM_ATENDIMENTO' && <p className="ticket-meta">Atendimento em andamento</p>}
            {canCancel && <button type="button" className="btn btn-light ticket-action" onClick={() => setConfirmCancel(true)}>Sair da fila e cancelar minha senha</button>}
            {ended && <button type="button" className="btn btn-light ticket-action" onClick={novaSenha}>Retirar nova senha</button>}
          </section>
        )}
        <section className="queue-section" aria-labelledby="queue-title"><h2 id="queue-title" className="section-title">FILA DE HOJE</h2>
          {displayFila.length === 0 ? <div className="queue-empty">Nenhuma pessoa aguardando no momento.</div> : <ul className="queue-list">{displayFila.map((item, index) => <li className="queue-item" key={item.id || `${item.codigo}-${index}`}><strong>{item.codigo}</strong><span>{item.status === 'CHAMADO' ? `Chamada${item.mesaNumero ? ` · mesa ${item.mesaNumero}` : ''}` : 'Aguardando'}</span></li>)}</ul>}
        </section>
        <p className="footer-note">Mantenha esta página aberta para acompanhar sua senha. As atualizações são verificadas automaticamente.</p>
      </main>
      {confirmCancel && <div className="modal-backdrop" role="presentation" onMouseDown={(e) => { if (e.target === e.currentTarget) setConfirmCancel(false) }}><div role="dialog" aria-modal="true" aria-labelledby="cancel-title" className="modal"><h2 id="cancel-title">Cancelar a senha {ticket?.codigo}?</h2><p>Ao confirmar, você sairá da fila de atendimento.</p><div className="modal-actions"><button type="button" className="btn btn-light" onClick={() => setConfirmCancel(false)} disabled={cancelLoading}>Voltar</button><button type="button" className="btn btn-danger" onClick={cancelar} disabled={cancelLoading}>{cancelLoading ? 'Cancelando...' : 'Confirmar cancelamento'}</button></div></div></div>}
    </div>
  )
}

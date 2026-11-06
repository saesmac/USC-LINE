import { useCallback, useEffect, useState } from 'react'
import { atendenteApi as api } from './atendenteApi'
import './AtendenteApp.css'
import { exportarRelatorioExcel } from './exportarExcel'

const today = () => {
  const now = new Date()
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`
}
const format = v => v == null ? '—' : Number.isFinite(Number(v)) ? `${Number(v).toFixed(1)} min` : '—'
const list = (data, keys = []) => Array.isArray(data) ? data : keys.reduce((a, key) => a || (Array.isArray(data?.[key]) ? data[key] : null), null) || []
const mesaId = m => Number(m.numero ?? m.mesaNumero)
const currentTicket = m => m?.senhaAtual || m?.ticketAtual || m?.senha || m?.ticket || m?.atendimentoAtual || null
const ticketStatus = t => String(t?.status || '').toUpperCase()

export default function AtendenteApp() {
  const [mesaNumero, setMesaNumero] = useState(() => Number(localStorage.getItem('unisagrado-mesa') || 1))
  const [fila, setFila] = useState([])
  const [mesas, setMesas] = useState([])
  const [relatorio, setRelatorio] = useState(null)
  const [historico, setHistorico] = useState([])
  const [inicio, setInicio] = useState(today)
  const [fim, setFim] = useState(today)
  const [periodoGerado, setPeriodoGerado] = useState(null)
  const [reportLoading, setReportLoading] = useState(false)
  const [excelLoading, setExcelLoading] = useState(false)
  const [reportMessage, setReportMessage] = useState('')
  const [view, setView] = useState('painel')
  const [erro, setErro] = useState('')
  const [sucesso, setSucesso] = useState('')
  const [busy, setBusy] = useState(false)
  const [modal, setModal] = useState(null)
  const [resultado, setResultado] = useState('Atendimento realizado')
  const [observacao, setObservacao] = useState('')

  const refresh = useCallback(async (showError = false) => {
    const responses = await Promise.allSettled([api.fila(), api.mesas()])
    if (responses[0].status === 'fulfilled') setFila(list(responses[0].value, ['senhas', 'tickets']))
    if (responses[1].status === 'fulfilled') setMesas(list(responses[1].value, ['mesas']))
    if (showError && responses.some(r => r.status === 'rejected')) setErro('Não foi possível atualizar os dados da fila ou das mesas. Confira a API.')
    if (responses.every(r => r.status === 'fulfilled')) setErro('')
  }, [])
  async function refreshReports() {
    if (!inicio || !fim || inicio > fim) {
      setErro('Informe um período válido (data inicial não pode ser posterior à final).')
      return
    }
    setReportLoading(true)
    setErro('')
    setReportMessage('')
    try {
      const [resumoData, historicoData] = await Promise.all([
        api.relatorio(inicio, fim),
        api.historico(inicio, fim),
      ])
      const registros = list(historicoData, ['atendimentos', 'historico', 'registros', 'tickets'])
      setRelatorio(resumoData)
      setHistorico(registros)
      setPeriodoGerado({ inicio, fim })
      setReportMessage(`Relatório atualizado: ${registros.length} registro(s) no período.`)
    } catch (e) {
      setRelatorio(null)
      setHistorico([])
      setPeriodoGerado(null)
      setErro(e.message || 'Não foi possível carregar o relatório. Confira a API.')
    } finally {
      setReportLoading(false)
    }
  }
  async function baixarExcel() {
    const datas = periodoGerado
    if (!datas || datas.inicio !== inicio || datas.fim !== fim) {
      setErro('Clique em Gerar relatório para aplicar as datas escolhidas antes de baixar o Excel.')
      return
    }
    setExcelLoading(true)
    setErro('')
    try {
      await exportarRelatorioExcel(api.csvUrl(datas.inicio, datas.fim), datas.inicio, datas.fim)
    } catch (e) {
      setErro(e.message || 'Não foi possível exportar a planilha.')
    } finally {
      setExcelLoading(false)
    }
  }

  useEffect(() => {
    refresh(true)
    const timer = setInterval(() => { if (!document.hidden) refresh() }, 4000)
    return () => clearInterval(timer)
  }, [refresh])
  useEffect(() => {
    if (view === 'relatorios' && !periodoGerado) {
      // Consulta inicial; depois o usuário aplica as datas explicitamente no botão.
      refreshReports()
    }
    // Executar somente quando entrar na tela de relatórios.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [view])
  useEffect(() => { localStorage.setItem('unisagrado-mesa', String(mesaNumero)) }, [mesaNumero])

  const selecionada = mesas.find(m => mesaId(m) === mesaNumero)
  const atual = currentTicket(selecionada)
  const aguardando = fila.filter(s => ticketStatus(s) === 'AGUARDANDO' || !s.status)
  const ordenada = [...aguardando].sort((a, b) => Number(!!b.preferencial) - Number(!!a.preferencial) || new Date(a.criadoEm || 0) - new Date(b.criadoEm || 0))
  const proxima = ordenada[0]
  const outrasOcupadas = mesas.filter(m => currentTicket(m) && ['CHAMADO', 'EM_ATENDIMENTO'].includes(ticketStatus(currentTicket(m))))
  const ocupada = !!atual && ['CHAMADO', 'EM_ATENDIMENTO'].includes(ticketStatus(atual))
  const resumo = relatorio?.resumo || {}

  async function run(action, ok) {
    if (busy) return
    setBusy(true); setErro(''); setSucesso('')
    try {
      await action()
      await refresh()
      setSucesso(ok)
      setModal(null)
    } catch (e) { setErro(e.message || 'Não foi possível concluir a operação.') }
    finally { setBusy(false) }
  }
  function finalizar() {
    if (!resultado.trim()) { setErro('Informe o resultado do atendimento.'); return }
    run(() => api.finalizar(mesaNumero, resultado.trim(), observacao.trim()), 'Atendimento finalizado e registrado.')
  }
  return <div className="att-app">
    <header className="att-header"><div className="att-header-inner">
      <div><span className="att-overline">UNISAGRADO</span><h1>{view === 'painel' ? 'Painel do atendente' : 'Relatórios por período'}</h1></div>
      <nav className="att-nav" aria-label="Navegação do atendente">
        <label className="att-mesa-label" htmlFor="att-mesa">Mesa <select id="att-mesa" value={mesaNumero} onChange={e => setMesaNumero(Number(e.target.value))}>{[1,2,3,4,5,6].map(n => <option key={n} value={n}>{n}</option>)}</select></label>
        <button className={view === 'painel' ? 'att-link active' : 'att-link'} onClick={() => setView('painel')}>Atendimento</button>
        <button className={view === 'relatorios' ? 'att-link active' : 'att-link'} onClick={() => setView('relatorios')}>Relatórios</button>
        <a className="att-link" href="/">Tela do aluno</a>
      </nav></div></header>
    <main className="att-container">
      {erro && <div className="att-notice att-error" role="alert">{erro}</div>}
      {sucesso && <div className="att-notice att-ok" role="status">{sucesso}</div>}
      {view === 'painel' ? <>
        <section className="att-stats" aria-label="Indicadores de atendimento">
          <div className="att-stat"><small>AGUARDANDO</small><strong>{aguardando.length}</strong></div>
          <div className="att-stat"><small>MESAS OCUPADAS</small><strong>{outrasOcupadas.length}</strong></div>
          <div className="att-stat"><small>MESA SELECIONADA</small><strong>{String(mesaNumero).padStart(2, '0')}</strong></div>
        </section>
        <section className="att-panel att-next"><div><div className="att-heading">{ocupada ? 'ATENDIMENTO DA MESA' : 'PRÓXIMA DA FILA'}</div>
          <div className="att-big-ticket">{ocupada ? atual.codigo : proxima?.codigo || '—'}</div>
          <div className="att-muted">{ocupada ? `${atual.nome || 'Aluno'} · ${ticketStatus(atual)}` : proxima ? `${proxima.nome || 'Aluno'}${proxima.preferencial ? ' · Preferencial' : ''}` : 'Nenhuma senha aguardando'}</div>
          </div><div className="att-actions">
          {ocupada ? ticketStatus(atual) === 'CHAMADO' ? <button className="att-primary" disabled={busy} onClick={() => run(() => api.iniciar(mesaNumero), 'Atendimento iniciado.')}>Iniciar atendimento</button> : <button className="att-primary" disabled={busy} onClick={() => {setResultado('Atendimento realizado'); setObservacao(''); setModal({tipo:'finalizar'})}}>Finalizar atendimento</button>
            : <button className="att-primary" disabled={busy || !proxima} onClick={() => run(() => api.chamar(mesaNumero), 'Próxima senha chamada.')}>Chamar para a mesa {mesaNumero}</button>}
          <button className="att-secondary" disabled={busy} onClick={() => refresh(true)}>Atualizar</button>
        </div></section>
        {ocupada && <section className="att-block"><h2>EM ATENDIMENTO / CHAMADA</h2><div className="att-person"><span className="att-pill">{atual.codigo}</span><div className="att-person-desc"><strong>{atual.nome || 'Aluno'}</strong><span>{atual.descricao || 'Sem descrição'}</span></div><span className="att-muted">Mesa {mesaNumero}</span></div></section>}
        <section className="att-block"><h2>AGUARDANDO</h2>{ordenada.length === 0 ? <div className="att-empty">Ninguém na fila agora.</div> : <div className="att-list">{ordenada.map(s => <div key={s.id || s.codigo} className="att-person"><span className="att-pill">{s.codigo}</span><div className="att-person-desc"><strong>{s.nome || 'Aluno'} {s.preferencial && <em>Preferencial</em>}</strong><span>{s.descricao || 'Sem descrição'}</span></div><button className="att-small" disabled={busy || ocupada} title={ocupada ? 'Finalize o atendimento atual antes de chamar outra senha' : 'Chamada manual'} onClick={() => run(() => api.chamarManual(s.codigo, mesaNumero), `Senha ${s.codigo} chamada.`)}>Chamar esta senha</button></div>)}</div>}</section>
        <section className="att-panel att-report-short"><div className="att-report-header"><h2>RELATÓRIOS E HISTÓRICO</h2><button className="att-secondary" onClick={() => setView('relatorios')}>Abrir relatórios</button></div><p className="att-muted">Consulte os indicadores, o histórico de atendimento e baixe a planilha CSV do Supabase.</p></section>
      </> : <>
        <section className="att-panel att-filter"><label>De<input type="date" value={inicio} max={fim} onChange={e => setInicio(e.target.value)} /></label><label>Até<input type="date" value={fim} min={inicio} onChange={e => setFim(e.target.value)} /></label><button className="att-primary" disabled={reportLoading || !inicio || !fim || inicio > fim} onClick={refreshReports}>{reportLoading ? 'Gerando...' : 'Gerar relatório'}</button><button className="att-secondary att-download" type="button" disabled={excelLoading || reportLoading || !periodoGerado || periodoGerado.inicio !== inicio || periodoGerado.fim !== fim} onClick={baixarExcel}>{excelLoading ? 'Preparando Excel...' : 'Baixar planilha Excel (.xlsx)'}</button></section>
        {reportMessage && <div className="att-notice att-ok" role="status">{reportMessage}</div>}
        {periodoGerado && (periodoGerado.inicio !== inicio || periodoGerado.fim !== fim) && <div className="att-notice att-pending">As datas foram alteradas. Clique em “Gerar relatório” para aplicar o novo período.</div>}
        <section className="att-panel att-report-short"><h2>{periodoGerado ? `${periodoGerado.inicio.split('-').reverse().join('/')} A ${periodoGerado.fim.split('-').reverse().join('/')}` : 'SELECIONE UM PERÍODO'}</h2><div className="att-stats report"><div className="att-stat"><small>SENHAS EMITIDAS</small><strong>{resumo.totalSenhas ?? '—'}</strong></div><div className="att-stat"><small>FINALIZADAS</small><strong>{resumo.finalizadas ?? '—'}</strong></div><div className="att-stat"><small>ESPERA MÉDIA</small><strong>{format(resumo.esperaMediaMinutos)}</strong></div><div className="att-stat"><small>ATENDIMENTO MÉDIO</small><strong>{format(resumo.atendimentoMedioMinutos)}</strong></div></div><h2 className="att-history-title">HISTÓRICO</h2><div className="att-table-scroll"><table className="att-table"><thead><tr><th>SENHA</th><th>ALUNO</th><th>STATUS</th><th>MESA</th><th>RESULTADO</th></tr></thead><tbody>{historico.map((h, i) => <tr key={h.id || i}><td>{h.codigo || h.senha || '—'}</td><td>{h.nome || '—'}</td><td>{h.status || '—'}</td><td>{h.mesaNumero ?? h.mesa?.numero ?? '—'}</td><td>{h.resultado || '—'}</td></tr>)}</tbody></table>{!historico.length && <div className="att-empty">Nenhum registro no período ou histórico ainda não carregado.</div>}</div></section>
      </>}
    </main>
    {modal?.tipo === 'finalizar' && <div className="att-modal-backdrop" role="presentation"><div className="att-modal" role="dialog" aria-modal="true" aria-labelledby="att-modal-title"><h2 id="att-modal-title">Finalizar atendimento de {atual?.codigo}</h2><p>{atual?.nome} · {atual?.descricao}</p><label>Resultado do atendimento<input value={resultado} onChange={e => setResultado(e.target.value)} placeholder="Ex.: Atendimento realizado" /></label><label>Comentário sobre o atendimento<textarea value={observacao} onChange={e => setObservacao(e.target.value)} rows="4" placeholder="Ex.: documentação entregue, aluno orientado a retornar amanhã..." /></label><div className="att-modal-buttons"><button className="att-secondary" disabled={busy} onClick={() => setModal(null)}>Cancelar</button><button className="att-primary" disabled={busy || !resultado.trim()} onClick={finalizar}>{busy ? 'Salvando...' : 'Concluir atendimento'}</button></div></div></div>}
  </div>
}

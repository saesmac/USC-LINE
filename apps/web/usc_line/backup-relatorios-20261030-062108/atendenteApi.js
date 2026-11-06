const BASE = (import.meta.env.VITE_API_URL || '').replace(/\/$/, '')

async function request(path, method = 'GET', body) {
  let response
  try {
    response = await fetch(`${BASE}/api${path}`, {
      method,
      cache: 'no-store',
      headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch { throw new Error('Não foi possível conectar à API. Confira se o backend está ligado na porta 5001.') }
  const raw = await response.text()
  let value
  try { value = raw ? JSON.parse(raw) : null } catch { value = raw }
  if (!response.ok) {
    const message = typeof value === 'object' && value !== null
      ? value.mensagem || value.message || value.erro || value.error || value.title || value.detail
      : null
    throw new Error(message || `Falha na operação (HTTP ${response.status}).`)
  }
  return value
}
export const atendenteApi = {
  fila: () => request('/Senhas'),
  mesas: () => request('/Mesas'),
  chamar: mesaNumero => request('/Senhas/chamar', 'POST', { mesaNumero }),
  chamarManual: (codigo, mesaNumero) => request('/Senhas/chamar-manual', 'POST', { codigo, mesaNumero }),
  iniciar: mesaNumero => request('/Senhas/iniciar', 'POST', { mesaNumero }),
  finalizar: (mesaNumero, resultado, observacao) => request('/Senhas/finalizar', 'POST', { mesaNumero, resultado, observacao }),
  relatorio: (dataInicio, dataFim) => request(`/Relatorios?dataInicio=${encodeURIComponent(dataInicio)}&dataFim=${encodeURIComponent(dataFim)}`),
  historico: (dataInicio, dataFim) => request(`/Historico?dataInicio=${encodeURIComponent(dataInicio)}&dataFim=${encodeURIComponent(dataFim)}`),
  csvUrl: (dataInicio, dataFim) => `${BASE}/api/Relatorios/csv?dataInicio=${encodeURIComponent(dataInicio)}&dataFim=${encodeURIComponent(dataFim)}`,
}

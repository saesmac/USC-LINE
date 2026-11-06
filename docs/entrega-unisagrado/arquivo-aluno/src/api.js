const API_BASE = (import.meta.env.VITE_API_URL || '').replace(/\/$/, '')

async function request(path, options = {}) {
  let response
  try {
    response = await fetch(`${API_BASE}/api${path}`, {
      ...options,
      headers: { ...(options.body ? { 'Content-Type': 'application/json' } : {}), ...options.headers },
      cache: 'no-store',
    })
  } catch {
    throw new Error('Não foi possível conectar à Secretaria. Confira sua conexão e tente novamente.')
  }
  const text = await response.text()
  let data
  try { data = text ? JSON.parse(text) : null } catch { data = text }
  if (!response.ok) {
    const message = typeof data === 'object' && data !== null
      ? data.mensagem || data.message || data.erro || data.error || data.title || data.detail
      : null
    throw new Error(message || `Não foi possível concluir a operação (${response.status}).`)
  }
  return data
}

export const alunoApi = {
  solicitar: ({ nome, descricao, preferencial }) => request('/Senhas', {
    method: 'POST', body: JSON.stringify({ nome, descricao, preferencial }),
  }),
  acompanhar: (codigo) => request(`/Acompanhamento/${encodeURIComponent(codigo)}`),
  cancelar: (codigo) => request('/Senhas/cancelar', {
    method: 'POST', body: JSON.stringify({ codigo, motivo: 'Cancelado pelo aluno' }),
  }),
  fila: () => request('/Senhas'),
}

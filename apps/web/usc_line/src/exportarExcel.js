// Exporta os mesmos registros do endpoint CSV, com estilos reais de Excel.
import ExcelJS from 'exceljs'

function parseCsv(text) {
  const rows = []
  let row = [], field = '', quoted = false
  const source = text.replace(/^\uFEFF/, '')
  for (let i = 0; i < source.length; i++) {
    const ch = source[i]
    if (ch === '"') {
      if (quoted && source[i + 1] === '"') { field += '"'; i++ }
      else quoted = !quoted
    } else if (ch === ';' && !quoted) {
      row.push(field); field = ''
    } else if ((ch === '\n' || ch === '\r') && !quoted) {
      if (ch === '\r' && source[i + 1] === '\n') i++
      row.push(field); field = ''
      if (row.some(v => v !== '')) rows.push(row)
      row = []
    } else field += ch
  }
  if (field !== '' || row.length) {
    row.push(field)
    if (row.some(v => v !== '')) rows.push(row)
  }
  return rows
}

function excelDate(v) {
  // O CSV do backend usa DD/MM/AAAA HH:mm:ss. Criamos datas locais, sem conversão UTC.
  const match = String(v || '').match(/^(\d{2})\/(\d{2})\/(\d{4})(?:\s+(\d{2}):(\d{2})(?::(\d{2}))?)?$/)
  if (!match) return v
  const [, d, m, y, hh = '0', mm = '0', ss = '0'] = match
  const date = new Date(+y, +m - 1, +d, +hh, +mm, +ss)
  return Number.isNaN(date.getTime()) ? v : date
}

export async function exportarRelatorioExcel(csvUrl, inicio, fim) {
  const response = await fetch(csvUrl, { cache: 'no-store' })
  if (!response.ok) throw new Error(`Não foi possível obter os dados da planilha (HTTP ${response.status}).`)
  const content = await response.text()
  const rows = parseCsv(content)
  if (!rows.length) throw new Error('A API retornou uma planilha vazia.')

  const workbook = new ExcelJS.Workbook()
  workbook.creator = 'Secretaria UNISAGRADO'
  workbook.created = new Date()
  const sheet = workbook.addWorksheet('Atendimentos', { views: [{ state: 'frozen', ySplit: 4 }] })
  const columnCount = rows[0].length
  sheet.mergeCells(1, 1, 1, columnCount)
  const title = sheet.getCell(1, 1)
  title.value = 'SECRETARIA UNISAGRADO | RELATÓRIO DE ATENDIMENTOS'
  title.font = { bold: true, color: { argb: 'FFFFFFFF' }, size: 15 }
  title.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF124F8C' } }
  title.alignment = { vertical: 'middle' }
  sheet.getRow(1).height = 36
  sheet.mergeCells(2, 1, 2, columnCount)
  sheet.getCell(2, 1).value = `Período: ${inicio.split('-').reverse().join('/')} a ${fim.split('-').reverse().join('/')}`
  sheet.getCell(2, 1).font = { color: { argb: 'FF415977' }, italic: true }
  sheet.getRow(2).height = 25
  sheet.addRow([])
  sheet.addRow(rows[0])
  const header = sheet.getRow(4)
  header.height = 32
  header.eachCell(cell => {
    cell.font = { bold: true, color: { argb: 'FFFFFFFF' } }
    cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF1662A6' } }
    cell.alignment = { vertical: 'middle', wrapText: true }
  })
  const dateColumns = new Set(rows[0].map((name, index) => /(?:Criada em|Chamada em|Início do atendimento|Fim do atendimento)/i.test(name) ? index + 1 : 0).filter(Boolean))
  rows.slice(1).forEach((values, idx) => {
    const row = sheet.addRow(values.map((v, i) => dateColumns.has(i + 1) && v ? excelDate(v) : v))
    row.height = 25
    row.eachCell({ includeEmpty: true }, (cell, col) => {
      cell.alignment = { vertical: 'middle', wrapText: true }
      if (dateColumns.has(col) && cell.value instanceof Date) cell.numFmt = 'dd/mm/yyyy hh:mm:ss'
      if (idx % 2 === 1) cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FFF0F6FC' } }
    })
  })
  const widths = [13, 30, 46, 17, 19, 23, 23, 25, 25, 13, 38, 48]
  for (let col = 1; col <= columnCount; col++) sheet.getColumn(col).width = widths[col - 1] || 24
  sheet.autoFilter = { from: { row: 4, column: 1 }, to: { row: Math.max(4, sheet.rowCount), column: columnCount } }
  sheet.pageSetup = { fitToPage: true, fitToWidth: 1, fitToHeight: 0, orientation: 'landscape' }
  const blob = new Blob([await workbook.xlsx.writeBuffer()], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `relatorio-unisagrado-${inicio}-a-${fim}.xlsx`
  document.body.appendChild(link)
  link.click()
  link.remove()
  setTimeout(() => URL.revokeObjectURL(url), 60000)
}

import { XMLParser } from 'fast-xml-parser';
import * as fs from 'fs';
import * as path from 'path';

export interface RdlcPage {
  width: string;
  height: string;
  leftMargin: string;
  rightMargin: string;
  topMargin: string;
  bottomMargin: string;
}

export interface RdlcElement {
  name: string;
  type: 'textbox' | 'line' | 'rectangle';
  top: string;
  left: string;
  width: string;
  height: string;
  value: string;       // e.g. "=Fields!Name.Value" or static text
  fieldName: string;   // extracted field name (e.g. "Name")
  fontFamily: string;
  fontSize: string;
  fontWeight: string;
  textAlign: string;
  writingMode: string;
  zIndex: number;
}

export interface RdlcTemplate {
  name: string;
  page: RdlcPage;
  elements: RdlcElement[];
  fields: string[];
}

export function parseRdlc(filePath: string): RdlcTemplate {
  const xml = fs.readFileSync(filePath, 'utf-8');
  const parser = new XMLParser({
    ignoreAttributes: false,
    attributeNamePrefix: '@_',
    textNodeName: '#text',
  });
  const doc = parser.parse(xml);
  const report = doc.Report;

  // Page settings
  const pg = report.Page || {};
  const page: RdlcPage = {
    width: pg.PageWidth || '21cm',
    height: pg.PageHeight || '29.7cm',
    leftMargin: pg.LeftMargin || '0cm',
    rightMargin: pg.RightMargin || '0cm',
    topMargin: pg.TopMargin || '0cm',
    bottomMargin: pg.BottomMargin || '0cm',
  };

  // Collect all elements recursively
  const elements: RdlcElement[] = [];
  const fields = new Set<string>();

  function extractElements(items: any): void {
    if (!items) return;

    // Textboxes
    const textboxes = toArray(items.Textbox);
    for (const tb of textboxes) {
      const el = parseTextbox(tb);
      elements.push(el);
      if (el.fieldName) fields.add(el.fieldName);
    }

    // Lines
    const lines = toArray(items.Line);
    for (const ln of lines) {
      elements.push({
        name: ln['@_Name'] || '',
        type: 'line',
        top: ln.Top || '0cm',
        left: ln.Left || '0cm',
        width: ln.Width || '0cm',
        height: ln.Height || '0cm',
        value: '',
        fieldName: '',
        fontFamily: '',
        fontSize: '',
        fontWeight: '',
        textAlign: '',
        writingMode: '',
        zIndex: parseInt(ln.ZIndex) || 0,
      });
    }

    // Rectangles (recurse into children)
    const rects = toArray(items.Rectangle);
    for (const rect of rects) {
      if (rect.ReportItems) {
        extractElements(rect.ReportItems);
      }
    }

    // Tablix (recurse into cells)
    const tablixes = toArray(items.Tablix);
    for (const tablix of tablixes) {
      const rows = tablix?.TablixBody?.TablixRows?.TablixRow;
      for (const row of toArray(rows)) {
        const cells = row?.TablixCells?.TablixCell;
        for (const cell of toArray(cells)) {
          const contents = cell?.CellContents;
          if (contents) {
            extractElements(contents);
          }
        }
      }
    }
  }

  extractElements(report?.Body?.ReportItems);

  return {
    name: path.basename(filePath, '.rdlc'),
    page,
    elements: elements.sort((a, b) => a.zIndex - b.zIndex),
    fields: [...fields],
  };
}

function parseTextbox(tb: any): RdlcElement {
  const para = tb?.Paragraphs?.Paragraph;
  const textRun = para?.TextRuns?.TextRun;
  const runStyle = textRun?.Style || {};
  const paraStyle = para?.Style || {};
  const tbStyle = tb?.Style || {};

  let value = '';
  if (typeof textRun?.Value === 'string') {
    value = textRun.Value;
  } else if (textRun?.Value?.['#text']) {
    value = textRun.Value['#text'];
  }

  // Extract field name from "=Fields!Name.Value"
  let fieldName = '';
  const match = value.match(/=Fields!(\w+)\.Value/);
  if (match) {
    fieldName = match[1];
  }

  return {
    name: tb['@_Name'] || '',
    type: 'textbox',
    top: tb.Top || '0cm',
    left: tb.Left || '0cm',
    width: tb.Width || '0cm',
    height: tb.Height || '0cm',
    value,
    fieldName,
    fontFamily: runStyle.FontFamily || '標楷體',
    fontSize: runStyle.FontSize || '10pt',
    fontWeight: runStyle.FontWeight || 'Normal',
    textAlign: paraStyle.TextAlign || tbStyle.TextAlign || '',
    writingMode: tbStyle.WritingMode || '',
    zIndex: parseInt(tb.ZIndex) || 0,
  };
}

function toArray(val: any): any[] {
  if (!val) return [];
  return Array.isArray(val) ? val : [val];
}

/**
 * Parse all RDLC files from source directory
 */
export function parseAllRdlc(sourceDir: string): RdlcTemplate[] {
  const files = fs.readdirSync(sourceDir).filter((f) => f.endsWith('.rdlc'));
  return files.map((f) => parseRdlc(path.join(sourceDir, f)));
}

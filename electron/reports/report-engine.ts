import * as Handlebars from 'handlebars';
import * as path from 'path';
import * as fs from 'fs';
import { parseRdlc, type RdlcTemplate, type RdlcElement } from './rdlc-parser';

// Cache parsed templates
const templateCache = new Map<string, RdlcTemplate>();

function getRdlcTemplate(templateName: string): RdlcTemplate {
  if (templateCache.has(templateName)) {
    return templateCache.get(templateName)!;
  }
  const rdlcDir = path.join('D:', 'appsystems', 'Ceremony', 'Ceremony');
  const filePath = path.join(rdlcDir, `${templateName}.rdlc`);
  const tmpl = parseRdlc(filePath);
  templateCache.set(templateName, tmpl);
  return tmpl;
}

function elementToHtml(el: RdlcElement, data: Record<string, any>): string {
  if (el.type === 'line') {
    const isHorizontal = el.height === '0cm';
    const borderStyle = 'border-top: 1px solid #000;';
    return `<div style="position:absolute;top:${el.top};left:${el.left};width:${el.width};height:${el.height};${isHorizontal ? borderStyle : 'border-left:1px solid #000;'}"></div>`;
  }

  // Textbox
  let content = '';
  if (el.fieldName && data[el.fieldName] !== undefined && data[el.fieldName] !== null) {
    content = String(data[el.fieldName]);
  } else if (!el.fieldName && el.value && !el.value.startsWith('=')) {
    content = el.value; // Static text
  }

  const styles: string[] = [
    `position:absolute`,
    `top:${el.top}`,
    `left:${el.left}`,
    `width:${el.width}`,
    `height:${el.height}`,
    `font-family:'${el.fontFamily}','DFKai-SB',serif`,
    `font-size:${el.fontSize}`,
    `overflow:hidden`,
    `box-sizing:border-box`,
    `padding:2pt`,
  ];

  if (el.fontWeight === 'Bold') styles.push('font-weight:bold');
  if (el.textAlign) styles.push(`text-align:${el.textAlign.toLowerCase()}`);
  if (el.writingMode === 'tb-rl' || el.writingMode === 'Vertical') {
    styles.push('writing-mode:vertical-rl', 'text-orientation:upright');
  }

  return `<div style="${styles.join(';')}">${Handlebars.escapeExpression(content)}</div>`;
}

export function generateReportHtml(
  templateName: string,
  dataRows: Record<string, any>[],
): string {
  const tmpl = getRdlcTemplate(templateName);
  const pages: string[] = [];

  for (const row of dataRows) {
    const elements = tmpl.elements.map((el) => elementToHtml(el, row)).join('\n');
    pages.push(`
      <div class="page" style="
        position:relative;
        width:${tmpl.page.width};
        height:${tmpl.page.height};
        margin:0;
        padding:0;
        overflow:hidden;
        page-break-after:always;
      ">
        ${elements}
      </div>
    `);
  }

  return `<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <style>
    @page {
      size: ${tmpl.page.width} ${tmpl.page.height};
      margin: 0;
    }
    * { margin: 0; padding: 0; }
    body { font-family: '標楷體', 'DFKai-SB', serif; }
    .page:last-child { page-break-after: auto; }
    @media print {
      body { -webkit-print-color-adjust: exact; }
    }
  </style>
</head>
<body>
  ${pages.join('\n')}
</body>
</html>`;
}

export function getTemplateInfo(templateName: string): {
  page: { width: string; height: string };
  fields: string[];
} {
  const tmpl = getRdlcTemplate(templateName);
  return { page: { width: tmpl.page.width, height: tmpl.page.height }, fields: tmpl.fields };
}

/**
 * List available RDLC templates
 */
export function listTemplates(): string[] {
  const rdlcDir = path.join('D:', 'appsystems', 'Ceremony', 'Ceremony');
  try {
    return fs
      .readdirSync(rdlcDir)
      .filter((f) => f.endsWith('.rdlc'))
      .map((f) => f.replace('.rdlc', ''));
  } catch {
    return [];
  }
}

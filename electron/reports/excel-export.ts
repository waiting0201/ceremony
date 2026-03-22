import * as ExcelJS from 'exceljs';
import * as path from 'path';

export async function exportToExcel(
  data: Record<string, any>[],
  columns: { header: string; key: string; width?: number }[],
  filePath: string,
  sheetName: string = '報表',
): Promise<void> {
  const workbook = new ExcelJS.Workbook();
  const sheet = workbook.addWorksheet(sheetName);

  sheet.columns = columns.map((c) => ({
    header: c.header,
    key: c.key,
    width: c.width || 15,
  }));

  // Header style
  sheet.getRow(1).font = { bold: true };
  sheet.getRow(1).fill = {
    type: 'pattern',
    pattern: 'solid',
    fgColor: { argb: 'FF4472C4' },
  };
  sheet.getRow(1).font = { bold: true, color: { argb: 'FFFFFFFF' } };

  for (const row of data) {
    sheet.addRow(row);
  }

  await workbook.xlsx.writeFile(filePath);
}

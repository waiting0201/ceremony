import { generateReportHtml, getTemplateInfo, listTemplates } from '../reports/report-engine';
import { exportToExcel } from '../reports/excel-export';
import * as path from 'path';
import * as fs from 'fs';

export function registerReportsIpc(ipcMain: any, getMainWindow: () => any): void {
  const { BrowserWindow, dialog } = require('electron');

  ipcMain.handle('reports:list', () => {
    return { success: true, data: listTemplates() };
  });

  ipcMain.handle('reports:info', (_e: any, templateName: string) => {
    try {
      const info = getTemplateInfo(templateName);
      return { success: true, data: info };
    } catch (e: any) {
      return { success: false, message: e.message };
    }
  });

  ipcMain.handle('reports:preview', async (_e: any, templateName: string, data: any[]) => {
    try {
      const html = generateReportHtml(templateName, data);
      // Write to temp file and return path
      const tmpDir = path.join(require('os').tmpdir(), 'ceremony-reports');
      if (!fs.existsSync(tmpDir)) fs.mkdirSync(tmpDir, { recursive: true });
      const tmpFile = path.join(tmpDir, `${templateName}-${Date.now()}.html`);
      fs.writeFileSync(tmpFile, html, 'utf-8');
      return { success: true, data: tmpFile };
    } catch (e: any) {
      return { success: false, message: e.message };
    }
  });

  ipcMain.handle('reports:print', async (_e: any, templateName: string, data: any[]) => {
    try {
      const html = generateReportHtml(templateName, data);
      const info = getTemplateInfo(templateName);

      // Create hidden window for printing
      const printWin = new BrowserWindow({
        show: false,
        webPreferences: { contextIsolation: true },
      });

      const tmpFile = path.join(require('os').tmpdir(), `ceremony-print-${Date.now()}.html`);
      fs.writeFileSync(tmpFile, html, 'utf-8');
      await printWin.loadFile(tmpFile);

      // Parse page size for print options
      const width = parseCm(info.page.width);
      const height = parseCm(info.page.height);

      printWin.webContents.print({
        silent: false,
        pageSize: { width: width * 10000, height: height * 10000 }, // microns
        margins: { marginType: 'none' },
      });

      // Clean up after a delay
      setTimeout(() => {
        printWin.close();
        try { fs.unlinkSync(tmpFile); } catch {}
      }, 30000);

      return { success: true };
    } catch (e: any) {
      return { success: false, message: e.message };
    }
  });

  ipcMain.handle('reports:pdf', async (_e: any, templateName: string, data: any[]) => {
    try {
      const info = getTemplateInfo(templateName);
      const html = generateReportHtml(templateName, data);

      const { filePath } = await dialog.showSaveDialog(getMainWindow(), {
        defaultPath: `${templateName}.pdf`,
        filters: [{ name: 'PDF', extensions: ['pdf'] }],
      });
      if (!filePath) return { success: false, message: '已取消' };

      const printWin = new BrowserWindow({
        show: false,
        webPreferences: { contextIsolation: true },
      });

      const tmpFile = path.join(require('os').tmpdir(), `ceremony-pdf-${Date.now()}.html`);
      fs.writeFileSync(tmpFile, html, 'utf-8');
      await printWin.loadFile(tmpFile);

      const width = parseCm(info.page.width);
      const height = parseCm(info.page.height);

      const pdfData = await printWin.webContents.printToPDF({
        pageSize: { width: width * 10000, height: height * 10000 },
        margins: { marginType: 'none' },
        printBackground: true,
      });

      fs.writeFileSync(filePath, pdfData);
      printWin.close();
      try { fs.unlinkSync(tmpFile); } catch {}

      return { success: true, data: filePath };
    } catch (e: any) {
      return { success: false, message: e.message };
    }
  });

  ipcMain.handle(
    'reports:excel',
    async (_e: any, data: any[], columns: any[], sheetName: string) => {
      try {
        const { filePath } = await dialog.showSaveDialog(getMainWindow(), {
          defaultPath: `${sheetName || '報表'}.xlsx`,
          filters: [{ name: 'Excel', extensions: ['xlsx'] }],
        });
        if (!filePath) return { success: false, message: '已取消' };

        await exportToExcel(data, columns, filePath, sheetName);
        return { success: true, data: filePath };
      } catch (e: any) {
        return { success: false, message: e.message };
      }
    },
  );
}

function parseCm(value: string): number {
  return parseFloat(value.replace('cm', '')) || 21;
}

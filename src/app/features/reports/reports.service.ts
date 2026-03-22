import { Injectable, inject } from '@angular/core';
import { IpcService } from '../../core/services/ipc.service';
import type { IpcResult } from '../../core/models/ipc-result.model';

@Injectable({ providedIn: 'root' })
export class ReportsService {
  private ipc = inject(IpcService);

  listTemplates(): Promise<IpcResult<string[]>> {
    return this.ipc.invoke('reports:list');
  }

  getInfo(templateName: string): Promise<IpcResult<{ page: { width: string; height: string }; fields: string[] }>> {
    return this.ipc.invoke('reports:info', templateName);
  }

  preview(templateName: string, data: any[]): Promise<IpcResult<string>> {
    return this.ipc.invoke('reports:preview', templateName, data);
  }

  print(templateName: string, data: any[]): Promise<IpcResult> {
    return this.ipc.invoke('reports:print', templateName, data);
  }

  exportPdf(templateName: string, data: any[]): Promise<IpcResult<string>> {
    return this.ipc.invoke('reports:pdf', templateName, data);
  }

  exportExcel(data: any[], columns: { header: string; key: string }[], sheetName: string): Promise<IpcResult<string>> {
    return this.ipc.invoke('reports:excel', data, columns, sheetName);
  }
}

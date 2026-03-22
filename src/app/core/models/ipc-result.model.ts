export interface IpcResult<T = void> {
  success: boolean;
  message?: string;
  data?: T;
  error?: string;
}

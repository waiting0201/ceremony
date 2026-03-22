export interface IResult<T = void> {
  success: boolean;
  message?: string;
  data?: T;
  error?: string;
}

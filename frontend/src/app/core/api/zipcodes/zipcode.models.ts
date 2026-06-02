export interface ZipcodeCitiesResponse {
  items: string[];
  total: number;
}

export interface ZipcodeAreaItem {
  zipcodeId: number;
  city: string;
  area: string;
  zipcode: string;
}

export interface ZipcodeAreasResponse {
  items: ZipcodeAreaItem[];
  total: number;
}

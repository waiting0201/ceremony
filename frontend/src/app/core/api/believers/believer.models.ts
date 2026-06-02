export interface BelieverListItem {
  id: string;
  employeeType: number;
  employeeTypeTitle: string;
  hallName: string | null;
  name: string;
  phone: string | null;
  isFixedNumber: boolean;
  mailZipcodeId: number | null;
  mailCity: string | null;
  mailArea: string | null;
  mailAddress: string | null;
  textZipcodeId: number | null;
  textCity: string | null;
  textArea: string | null;
  textAddress: string | null;
  livingNames: (string | null)[];
  deadNames: (string | null)[];
}

export interface BelieverListResponse {
  items: BelieverListItem[];
  total: number;
}

export interface BelieverSearchQuery {
  name?: string | null;
  phone?: string | null;
  hallName?: string | null;
  livingName?: string | null;
  deadName?: string | null;
}

export interface BelieverUpsertRequest {
  employeeType: number;
  name: string;
  mailAddress: string;
  hallName?: string | null;
  phone?: string | null;
  isFixedNumber?: boolean;
  mailZipcodeId?: number | null;
  textZipcodeId?: number | null;
  textAddress?: string | null;
  livingNames?: (string | null)[];
  deadNames?: (string | null)[];
}

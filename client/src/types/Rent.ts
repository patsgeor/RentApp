
export enum FieldDataType { Text = 0, Number = 1, Boolean = 2, Date = 3 }

export type AssetTypeField={
  id: string;
  name: string;      // π.χ. "isbn", "watt"
  label: string;     // π.χ. "ISBN Βιβλίου", "Ισχύς (Watt)"
  dataType: FieldDataType;
  isRequired: boolean;
}

export type AssetType = {
 id: string;
  name: string;
  fields: AssetTypeField[];
}
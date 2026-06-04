export type RentViewDto = {
  id: number
  notes?: string
  description: string
  logCategory: string
  createdByMemberName: string
  monadaName: string
  invoiceItemId: number
  serialNumber: string
  initialValue: number
  currentValue: number
  residualValue: number
  acquiredDate: string
  usefulLifeYears: number
  isLocked: boolean
  isDeleted: boolean
  createdAt?: string
  modifiedAt?: string
}
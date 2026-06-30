export type InviteCreds= {
  email: string
  firstName: string
  lastName: string
  role: string
}


export interface UserDto {
  id: string;
  email: string;
  displayName: string;
  tenantId: string;
  tenantName: string;
  token: string;
  roles?:string[];
}

export interface LoginDto {
  email: string;
  password: string;
}

export interface TenantRegisterDto {
  companyName: string;
  vatNumber?: string;
  contactInfo?: string;
  displayName: string;
  email: string;
  phoneNumber: string;
  firstName: string;
  lastName: string;
  password: string;
  confirmPassword: string;
}


export interface MemberInviteDto {
  email: string;
  firstName: string;
  lastName: string;
  role: string;
}

export interface MemberInviteInfoDto {
  email: string;
  firstName: string;
  lastName: string;
  tenantName: string;
}

export interface MemberRegisterFromInviteDto {
  token: string;
  displayName: string;
  password: string;
  confirmPassword: string;
}
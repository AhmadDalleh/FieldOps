export type Role = 'Admin' | 'Dispatcher' | 'Technician';

export interface AuthUser {
  id: string;
  email: string;
  fullName: string;
  role: Role;
  technicianId: string | null;
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: AuthUser;
}

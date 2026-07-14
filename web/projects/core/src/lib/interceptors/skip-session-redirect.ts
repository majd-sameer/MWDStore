import { HttpContextToken } from '@angular/common/http';


export const SKIP_SESSION_REDIRECT = new HttpContextToken<boolean>(() => false);

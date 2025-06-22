import { HttpInterceptorFn } from '@angular/common/http';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  // Get the auth token from localStorage
  const authToken = localStorage.getItem('authToken');
  
  // Clone the request and add headers
  let modifiedReq = req.clone({
    setHeaders: {
      // Add ngrok header to skip browser warning
      'ngrok-skip-browser-warning': 'true'
    }
  });

  // Add Authorization header if token exists
  if (authToken) {
    modifiedReq = modifiedReq.clone({
      setHeaders: {
        'Authorization': `Bearer ${authToken}`,
        'ngrok-skip-browser-warning': 'true'
      }
    });
  }

  return next(modifiedReq);
};
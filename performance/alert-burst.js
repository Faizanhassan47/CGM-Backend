import http from 'k6/http'; import { check } from 'k6';
export const options={vus:100,duration:'2m',thresholds:{http_req_failed:['rate<0.01'],http_req_duration:['p(95)<750']}};
export default function(){const r=http.post(`${__ENV.CGM_API_URL}/api/glucose/measurement`,JSON.stringify({sensorId:Number(__ENV.CGM_SENSOR_ID),glucoseValue:60,measurementTime:new Date().toISOString()}),{headers:{Authorization:`Bearer ${__ENV.CGM_ACCESS_TOKEN}`,'Content-Type':'application/json'}});check(r,{'queued':x=>x.status===200});}

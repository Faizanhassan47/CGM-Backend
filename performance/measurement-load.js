import http from 'k6/http'; import { check } from 'k6';
export const options={scenarios:{measurements:{executor:'constant-arrival-rate',rate:4,timeUnit:'1s',duration:'5m',preAllocatedVUs:50,maxVUs:1000}},thresholds:{http_req_failed:['rate<0.01'],http_req_duration:['p(95)<500']}};
const base=__ENV.CGM_API_URL,token=__ENV.CGM_ACCESS_TOKEN,sensor=Number(__ENV.CGM_SENSOR_ID);
export default function(){const r=http.post(`${base}/api/glucose/measurement`,JSON.stringify({sensorId:sensor,glucoseValue:120,measurementTime:new Date().toISOString()}),{headers:{Authorization:`Bearer ${token}`,'Content-Type':'application/json'}});check(r,{'accepted':x=>x.status===200});}

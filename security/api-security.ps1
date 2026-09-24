param([Parameter(Mandatory=$true)][string]$ApiUrl)
$ErrorActionPreference='Stop'
function Expect-Status([string]$Path,[int]$Expected){try{Invoke-WebRequest -Uri "$ApiUrl$Path" -UseBasicParsing|Out-Null;$actual=200}catch{$actual=[int]$_.Exception.Response.StatusCode};if($actual-ne $Expected){throw "$Path returned $actual; expected $Expected"}}
Expect-Status '/api/devices' 401
Expect-Status '/api/account/sessions' 401
Expect-Status '/api/alert-rules' 401
Write-Output 'Anonymous authorization checks passed.'

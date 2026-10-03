Param(
    [Parameter(Mandatory = $true)]
    [string] $InputFile,

    [Parameter(Mandatory = $true)]
    [string] $OutputFile,

    [Parameter(Mandatory = $true)]
    [string] $DueDate,

    [Parameter(Mandatory = $true)]
    [string] $Organization
)

Import-Module "$PSScriptRoot/InvitationRequest.psm1" -Force

$issue = Read-IssuePayload -Path $InputFile

$requestType = Get-IssueFormValue -Body $issue.body -Label '요청 유형'
$organisation = Get-IssueFormValue -Body $issue.body -Label '조직'
$githubHandle = Get-IssueFormValue -Body $issue.body -Label 'GitHub 핸들'

$invalidReasons = [System.Collections.Generic.List[string]]::new()

if ($requestType -ne 'GitHub 조직 초대 요청') {
    $invalidReasons.Add('요청 유형이 올바르지 않습니다.')
}

if ($organisation -ne $Organization) {
    $invalidReasons.Add('GitHub 조직이 올바르지 않습니다.')
}

if ((Test-GitHubHandle -Handle $githubHandle) -eq $false) {
    $invalidReasons.Add('GitHub 핸들이 올바르지 않습니다.')
}

Write-InvitationResult `
    -Issue $issue `
    -DueDate $DueDate `
    -OutputFile $OutputFile `
    -InvalidReasons $invalidReasons `
    -AuthorMismatchReason 'GitHub 핸들이 이슈 작성자와 일치하지 않습니다.' `
    -Body @{
        requestType = $requestType
        organisation = $organisation
        githubHandle = $githubHandle
        name = $null
        email = $null
    }

node {
  stage('SCM') {
    checkout scm
  }
  stage('SonarQube Analysis') {
    def scannerHome = tool 'sonar-dotnet'
    withSonarQubeEnv() {
      sh "dotnet ${scannerHome}/SonarScanner.MSBuild.dll begin /k:\"dotnet-scan-jenkins\""
      sh "dotnet build SynapseDynamicAPI/SynapseDynamicAPI/SynapseDynamicAPI.csproj"
      sh "dotnet ${scannerHome}/SonarScanner.MSBuild.dll end"
    }
  }
}
pipeline {
    agent any

    options {
        disableConcurrentBuilds()
        timestamps()
    }

    environment {
        REGISTRY = 'localhost:5000'
        KUBE_NAMESPACE = 'fundoo'
    }

    stages {
        stage('Build and publish images') {
            steps {
                sh '''
                    set -eu
                    docker build -f Dockerfile --build-arg PROJECT=Fundoo.User.Api/Fundoo.User.Api.csproj --build-arg ASSEMBLY=Fundoo.User.Api -t "$REGISTRY/fundoo-user:$BUILD_NUMBER" .
                    docker build -f Dockerfile --build-arg PROJECT=Fundoo.Notes.Api/Fundoo.Notes.Api.csproj --build-arg ASSEMBLY=Fundoo.Notes.Api -t "$REGISTRY/fundoo-notes:$BUILD_NUMBER" .
                    docker build -f Dockerfile --build-arg PROJECT=Fundoo.Notification.Api/Fundoo.Notification.Api.csproj --build-arg ASSEMBLY=Fundoo.Notification.Api -t "$REGISTRY/fundoo-notification:$BUILD_NUMBER" .
                    docker build -f Dockerfile --build-arg PROJECT=Fundoo.ApiGateway/Fundoo.ApiGateway.csproj --build-arg ASSEMBLY=Fundoo.ApiGateway -t "$REGISTRY/fundoo-gateway:$BUILD_NUMBER" .
                    docker push "$REGISTRY/fundoo-user:$BUILD_NUMBER"
                    docker push "$REGISTRY/fundoo-notes:$BUILD_NUMBER"
                    docker push "$REGISTRY/fundoo-notification:$BUILD_NUMBER"
                    docker push "$REGISTRY/fundoo-gateway:$BUILD_NUMBER"
                '''
            }
        }

        stage('Roll out to K3s') {
            steps {
                sh '''
                    set -eu
                    kubectl -n "$KUBE_NAMESPACE" set image deployment/user-api user-api="$REGISTRY/fundoo-user:$BUILD_NUMBER"
                    kubectl -n "$KUBE_NAMESPACE" set image deployment/notes-api notes-api="$REGISTRY/fundoo-notes:$BUILD_NUMBER"
                    kubectl -n "$KUBE_NAMESPACE" set image deployment/notification-api notification-api="$REGISTRY/fundoo-notification:$BUILD_NUMBER"
                    kubectl -n "$KUBE_NAMESPACE" set image deployment/api-gateway api-gateway="$REGISTRY/fundoo-gateway:$BUILD_NUMBER"
                    kubectl -n "$KUBE_NAMESPACE" rollout status deployment/user-api --timeout=180s
                    kubectl -n "$KUBE_NAMESPACE" rollout status deployment/notes-api --timeout=180s
                    kubectl -n "$KUBE_NAMESPACE" rollout status deployment/notification-api --timeout=180s
                    kubectl -n "$KUBE_NAMESPACE" rollout status deployment/api-gateway --timeout=180s
                '''
            }
        }
    }
}

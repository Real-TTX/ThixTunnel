# A minimal SSH server for the end-to-end test (scripts/e2e-docker.ps1):
#  - "tunnel": key login only, TCP forwarding allowed
#  - "setup":  password login (e2e-passwort) for the key setup test ("Beim ersten Anmelden Schlüssel anlegen")
FROM alpine:3
RUN apk add --no-cache openssh \
 && ssh-keygen -A \
 && adduser -D -s /bin/sh tunnel \
 && sed -i 's/^tunnel:!/tunnel:*/' /etc/shadow \
 && adduser -D -s /bin/sh setup \
 && echo 'setup:e2e-passwort' | chpasswd \
 && mkdir -p /home/tunnel/.ssh \
 && printf 'AllowTcpForwarding yes\nPasswordAuthentication no\nKbdInteractiveAuthentication no\nPermitRootLogin no\nMatch User setup\n    PasswordAuthentication yes\n' > /etc/ssh/sshd_config.d/thixtunnel.conf \
 && sed -i 's/^AllowTcpForwarding no/#AllowTcpForwarding no/' /etc/ssh/sshd_config
COPY authorized_keys /home/tunnel/.ssh/authorized_keys
RUN chown -R tunnel:tunnel /home/tunnel/.ssh && chmod 700 /home/tunnel/.ssh && chmod 600 /home/tunnel/.ssh/authorized_keys
EXPOSE 22
CMD ["/usr/sbin/sshd", "-D", "-e"]

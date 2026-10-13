#!/usr/bin/env bash
# The VPS firewall (OPS-1), as root. Safe to re-run; cron runs it weekly to pick up new Cloudflare ranges.
#  - ufw: deny incoming, SSH (rate-limited), HTTPS only from Cloudflare.
#  - Docker publishes Caddy's port through its own iptables chains, which skip ufw's INPUT rules, so the same
#    Cloudflare-only rule is applied in Docker's DOCKER-USER chain too.
set -euo pipefail
[ "$(id -u)" = 0 ] || { echo "Run as root" >&2; exit 1; }

v4="$(curl -fsS --max-time 20 https://www.cloudflare.com/ips-v4)"
v6="$(curl -fsS --max-time 20 https://www.cloudflare.com/ips-v6)"
# Refuse to apply an empty or odd list, which would cut off all traffic.
echo "$v4" | grep -Eqv '^[0-9.]+/[0-9]+$' && { echo "Unexpected Cloudflare IPv4 list" >&2; exit 1; }
echo "$v6" | grep -Eqv '^[0-9a-f:]+/[0-9]+$' && { echo "Unexpected Cloudflare IPv6 list" >&2; exit 1; }
[ "$(echo "$v4" | wc -l)" -ge 5 ] || { echo "Too few Cloudflare IPv4 ranges" >&2; exit 1; }
# The interface that faces the internet, and the port sshd really listens on.
wan="$(ip -4 route get 1.1.1.1 | awk '{for (i = 1; i < NF; i++) if ($i == "dev") { print $(i + 1); exit }}')"
ssh_port="$(sshd -T 2>/dev/null | awk '$1 == "port" { print $2; exit }')"
[ -n "$wan" ] || { echo "No route to the internet" >&2; exit 1; }
[ -n "$ssh_port" ] || ssh_port=22

ufw default deny incoming
ufw default allow outgoing
ufw limit "$ssh_port/tcp" comment ssh

# Add the current ranges first (ufw skips rules it already has), then remove ranges Cloudflare dropped,
# so HTTPS is never closed in between. Rules are deleted from the bottom because numbers shift.
for range in $v4 $v6; do ufw allow proto tcp from "$range" to any port 443 comment cloudflare >/dev/null; done
# shellcheck disable=SC2086,SC2116  # splits the newline-separated lists into one space-separated line on purpose
keep=" $(echo $v4 $v6) "
ufw status numbered \
  | sed -n 's/^\[ *\([0-9]*\)\].* ALLOW IN *\([^ ]*\) .*# cloudflare$/\1 \2/p' \
  | sort -rn | while read -r n source; do
      case "$keep" in *" $source "*) ;; *) ufw --force delete "$n" >/dev/null ;; esac
    done

docker_block() {   # $1: ranges
  echo "# BEGIN BASACHAI DOCKER"
  echo "*filter"
  echo ":DOCKER-USER - [0:0]"
  echo "-A DOCKER-USER -m conntrack --ctstate RELATED,ESTABLISHED -j RETURN"
  for range in $1; do echo "-A DOCKER-USER -i $wan -p tcp -m conntrack --ctorigdstport 443 -s $range -j RETURN"; done
  echo "-A DOCKER-USER -i $wan -m conntrack --ctstate NEW -j DROP"
  echo "-A DOCKER-USER -j RETURN"
  echo "COMMIT"
  echo "# END BASACHAI DOCKER"
}
replace_block() {   # $1: rules file, $2: ranges
  sed -i '/^# BEGIN BASACHAI DOCKER$/,/^# END BASACHAI DOCKER$/d' "$1"
  docker_block "$2" >> "$1"
}
replace_block /etc/ufw/after.rules "$v4"
replace_block /etc/ufw/after6.rules "$v6"

ufw --force enable
ufw reload
echo "Firewall applied: SSH on $ssh_port, and HTTPS from $(echo "$v4" "$v6" | wc -w) Cloudflare ranges on $wan"
